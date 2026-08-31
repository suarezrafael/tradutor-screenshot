using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenTranslator.Desktop.Interop;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Desktop;

/// <summary>
/// Full-virtual-desktop, click-drag region selector (like the Windows Snipping Tool).
///
/// DPI note: the window's HWND is placed at the exact physical pixel bounds of the virtual
/// desktop via SetWindowPos (see <see cref="OnSourceInitialized"/>), sidestepping WPF's per-monitor
/// DPI virtualization of Window.Left/Top/Width/Height, which cannot correctly size a single window
/// spanning monitors with different DPI scale factors. The actual selection measurement is taken
/// from <see cref="System.Windows.Forms.Cursor.Position"/> (always physical pixels for a
/// Per-Monitor-V2-aware process, see app.manifest), so the final captured region is pixel-accurate
/// regardless of DPI. WPF's own DPI scale is only used to draw the *visual* selection rectangle.
/// </summary>
public partial class SelectionOverlayWindow : Window
{
    private readonly BoundingBox _virtualScreenBounds;
    private System.Drawing.Point? _dragStart;

    public event Action<BoundingBox>? RegionSelected;
    public event Action? Cancelled;

    public SelectionOverlayWindow(BoundingBox virtualScreenBounds)
    {
        InitializeComponent();
        _virtualScreenBounds = virtualScreenBounds;

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            Canvas.SetLeft(HintBorder, 24);
            Canvas.SetTop(HintBorder, 24);
            Focus();
        };

        PreviewMouseLeftButtonDown += OnMouseDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseUp;
        PreviewKeyDown += OnKeyDown;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(
            hwnd,
            NativeMethods.HWND_TOPMOST,
            (int)Math.Round(_virtualScreenBounds.X),
            (int)Math.Round(_virtualScreenBounds.Y),
            (int)Math.Round(_virtualScreenBounds.Width),
            (int)Math.Round(_virtualScreenBounds.Height),
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = System.Windows.Forms.Cursor.Position;
        SelectionBorder.Visibility = Visibility.Visible;
        Mouse.Capture(this);
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        DrawSelection(start, System.Windows.Forms.Cursor.Position);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start)
        {
            return;
        }

        _dragStart = null;
        Mouse.Capture(null);

        var region = BuildBoundingBox(start, System.Windows.Forms.Cursor.Position);
        if (region.Width < 4 || region.Height < 4)
        {
            Cancelled?.Invoke();
        }
        else
        {
            RegionSelected?.Invoke(region);
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancelled?.Invoke();
        }
    }

    private void DrawSelection(System.Drawing.Point start, System.Drawing.Point current)
    {
        var dpi = VisualTreeHelper.GetDpi(this);

        double ToLocalX(int physicalX) => (physicalX - _virtualScreenBounds.X) / dpi.DpiScaleX;
        double ToLocalY(int physicalY) => (physicalY - _virtualScreenBounds.Y) / dpi.DpiScaleY;

        var left = ToLocalX(Math.Min(start.X, current.X));
        var top = ToLocalY(Math.Min(start.Y, current.Y));
        var right = ToLocalX(Math.Max(start.X, current.X));
        var bottom = ToLocalY(Math.Max(start.Y, current.Y));

        Canvas.SetLeft(SelectionBorder, left);
        Canvas.SetTop(SelectionBorder, top);
        SelectionBorder.Width = Math.Max(0, right - left);
        SelectionBorder.Height = Math.Max(0, bottom - top);
    }

    private static BoundingBox BuildBoundingBox(System.Drawing.Point a, System.Drawing.Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new BoundingBox(x, y, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
    }
}
