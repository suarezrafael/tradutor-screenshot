using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenTranslator.Application;
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

        Loaded += (_, _) => Render();
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
            // Sized to match the original text's own box exactly (OverTranslate-style overlay-in-place):
            // clipped with an ellipsis if the translation doesn't fit, with the full text always
            // available via tooltip, rather than trying to find room for it elsewhere.
            var label = new Border
            {
                Background = backgroundBrush,
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(2, 0, 2, 0),
                Width = block.TranslationBounds.Width / dpiScale,
                Height = block.TranslationBounds.Height / dpiScale,
                ClipToBounds = true,
                ToolTip = block.TranslatedText,
                Child = new TextBlock
                {
                    Text = block.TranslatedText,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = block.FontSize,
                    Foreground = textBrush,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };

            Canvas.SetLeft(label, block.TranslationBounds.X / dpiScale);
            Canvas.SetTop(label, block.TranslationBounds.Y / dpiScale);
            OverlayCanvas.Children.Add(label);
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
