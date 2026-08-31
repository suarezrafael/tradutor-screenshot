using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenTranslator.Desktop;

/// <summary>System tray icon with the "Capturar região / Última captura / Configurações / Sair" menu.</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event Action? CaptureRequested;
    public event Action? ShowLastResultRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Capturar região", null, (_, _) => CaptureRequested?.Invoke());
        menu.Items.Add("Última captura", null, (_, _) => ShowLastResultRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Configurações", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = "Screen Translator",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _notifyIcon.DoubleClick += (_, _) => CaptureRequested?.Invoke();
    }

    public void ShowBalloon(string title, string text) =>
        _notifyIcon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);

    /// <summary>Draws a small "A→" glyph at runtime so the app doesn't need to ship a binary .ico asset.</summary>
    private static Icon CreateAppIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var backgroundBrush = new SolidBrush(Color.FromArgb(255, 33, 115, 219));
            g.FillEllipse(backgroundBrush, 0, 0, 31, 31);
            using var font = new Font("Segoe UI", 14, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var text = "T";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, textBrush, (32 - size.Width) / 2, (32 - size.Height) / 2 - 1);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
