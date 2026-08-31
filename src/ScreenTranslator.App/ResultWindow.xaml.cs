using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenTranslator.Application;
using ScreenTranslator.Desktop.Interop;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Desktop;

/// <summary>Shows the captured screenshot with translation labels drawn above each original phrase.</summary>
public partial class ResultWindow : Window
{
    private readonly TranslationSessionResult _session;
    private readonly AppSettings _settings;
    private double _dpiScale = 1.0;

    public event Action? RecaptureRequested;

    public ResultWindow(TranslationSessionResult session, AppSettings settings)
    {
        InitializeComponent();
        _session = session;
        _settings = settings;

        Loaded += (_, _) =>
        {
            Render();
            // Forces layout to catch up with the sizing Render() just applied, so ActualWidth/Height
            // (read by PositionOverCapture) reflect the real content instead of stale pre-content values.
            UpdateLayout();
            PositionOverCapture();
        };
    }

    /// <summary>
    /// Opens the window so the captured *image* - not the window's own top-left, which has the button
    /// bar sitting above it - lands exactly where the region was captured from, like the screenshot is
    /// being overlaid back onto the real screen it came from. Clamped to the capture's own monitor's
    /// work area (in physical pixels, per the DPI note on SelectionOverlayWindow) so a capture near a
    /// screen edge doesn't push the window off-screen.
    /// </summary>
    private void PositionOverCapture()
    {
        var bounds = _session.Capture.ScreenBounds;
        var workArea = System.Windows.Forms.Screen
            .FromPoint(new System.Drawing.Point((int)bounds.X, (int)bounds.Y))
            .WorkingArea;

        // Where the image sits relative to the window's own origin (below the button bar), in DIUs -
        // converted to physical pixels so the window's top-left can be offset to compensate.
        var imageOffset = CaptureImage.TranslatePoint(new Point(0, 0), this);
        var imageOffsetXPx = imageOffset.X * _dpiScale;
        var imageOffsetYPx = imageOffset.Y * _dpiScale;

        var windowWidthPx = ActualWidth * _dpiScale;
        var windowHeightPx = ActualHeight * _dpiScale;

        var desiredLeft = bounds.X - imageOffsetXPx;
        var desiredTop = bounds.Y - imageOffsetYPx;

        var left = Math.Clamp(desiredLeft, workArea.Left, Math.Max(workArea.Left, workArea.Right - windowWidthPx));
        var top = Math.Clamp(desiredTop, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - windowHeightPx));

        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            (int)Math.Round(left),
            (int)Math.Round(top),
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private void Render()
    {
        _dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var dpiScale = _dpiScale;
        var displayWidth = _session.Capture.PixelWidth / dpiScale;
        var displayHeight = _session.Capture.PixelHeight / dpiScale;

        var bitmap = LoadBitmap(_session.Capture.ImageBytes);
        CaptureImage.Source = bitmap;
        CaptureImage.Width = displayWidth;
        CaptureImage.Height = displayHeight;
        ImageHost.Width = displayWidth;
        ImageHost.Height = displayHeight;

        var backgroundBrush = SolidBrush(_settings.OverlayBackgroundColor, _settings.OverlayBackgroundOpacity);
        var textBrush = SolidBrush(_settings.OverlayTextColor, _settings.OverlayBackgroundOpacity);

        foreach (var block in _session.Translations)
        {
            // Anchored at the original text's own top-left (OverTranslate-style overlay-in-place),
            // but sized to its natural content rather than clipped to the original's box: the full
            // translation must always be readable without hovering, even if that means it extends
            // past where the original text ended. MinWidth/MinHeight keep it at least as big as the
            // original so a short translation still fully covers what it's replacing.
            var label = new Border
            {
                Background = backgroundBrush,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0, 2, 0),
                MinWidth = block.TranslationBounds.Width / dpiScale,
                MinHeight = block.TranslationBounds.Height / dpiScale,
                Child = new TextBlock
                {
                    Text = block.TranslatedText,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = block.FontSize,
                    Foreground = textBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };

            Canvas.SetLeft(label, block.TranslationBounds.X / dpiScale);
            Canvas.SetTop(label, block.TranslationBounds.Y / dpiScale);
            OverlayCanvas.Children.Add(label);
        }
    }

    private void OnButtonBarMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // No native title bar (WindowStyle="None") to drag by, so the button bar itself doubles as one -
        // only when the click lands on the bar's own background, not on one of the buttons inside it.
        if (e.OriginalSource == sender)
        {
            DragMove();
        }
    }

    private static SolidColorBrush SolidBrush(string hexColor, double opacity)
    {
        var color = (Color)ColorConverter.ConvertFromString(hexColor)!;
        return new SolidColorBrush(color) { Opacity = opacity };
    }

    private static BitmapImage LoadBitmap(byte[] pngBytes)
    {
        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(pngBytes);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void OnCopyTextClick(object sender, RoutedEventArgs e)
    {
        var text = string.Join(Environment.NewLine, _session.Translations.Select(t => t.TranslatedText));
        if (text.Length > 0)
        {
            Clipboard.SetText(text);
        }
    }

    private void OnCopyImageClick(object sender, RoutedEventArgs e) =>
        Clipboard.SetImage((BitmapSource)CaptureImage.Source);

    private void OnCopyImageWithTranslationClick(object sender, RoutedEventArgs e) =>
        Clipboard.SetImage(RenderImageWithTranslation());

    private void OnSaveImageClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Imagem PNG (*.png)|*.png",
            FileName = $"screen-translator-{DateTime.Now:yyyyMMdd-HHmmss}.png",
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllBytes(dialog.FileName, _session.Capture.ImageBytes);
        }
    }

    private void OnSaveImageWithTranslationClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Imagem PNG (*.png)|*.png",
            FileName = $"screen-translator-traduzido-{DateTime.Now:yyyyMMdd-HHmmss}.png",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(RenderImageWithTranslation()));
        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
    }

    private RenderTargetBitmap RenderImageWithTranslation()
    {
        var dpi = 96.0 * _dpiScale;
        var renderBitmap = new RenderTargetBitmap(
            _session.Capture.PixelWidth, _session.Capture.PixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        renderBitmap.Render(ImageHost);
        renderBitmap.Freeze();
        return renderBitmap;
    }

    private void OnToggleOverlayClick(object sender, RoutedEventArgs e)
    {
        var showingOriginalOnly = ToggleOverlayButton.IsChecked != true;
        OverlayCanvas.Visibility = showingOriginalOnly ? Visibility.Collapsed : Visibility.Visible;
        ToggleOverlayButton.Content = showingOriginalOnly ? "Ver tradução" : "Ver original";
    }

    private void OnRecaptureClick(object sender, RoutedEventArgs e)
    {
        RecaptureRequested?.Invoke();
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
