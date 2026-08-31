using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Infrastructure.ScreenCapture;

/// <summary>
/// Captures screen pixels with GDI's BitBlt (via <see cref="Graphics.CopyFromScreen"/>). Coordinates are
/// always physical pixels of the virtual desktop: the app is marked Per-Monitor-V2 DPI aware (see
/// app.manifest), so WinForms/GDI already hand us physical coordinates without manual DPI math.
/// </summary>
public sealed class Win32ScreenCaptureService(ILogger<Win32ScreenCaptureService> logger) : IScreenCaptureService
{
    public BoundingBox GetVirtualScreenBounds()
    {
        var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
        return new BoundingBox(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public Task<CapturedRegion> CaptureRegionAsync(BoundingBox region, CancellationToken cancellationToken = default) =>
        Task.Run(() => Capture(region), cancellationToken);

    private CapturedRegion Capture(BoundingBox region)
    {
        var x = (int)Math.Round(region.X);
        var y = (int)Math.Round(region.Y);
        var width = Math.Max(1, (int)Math.Round(region.Width));
        var height = Math.Max(1, (int)Math.Round(region.Height));

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);

        logger.LogDebug("Captured region ({X},{Y}) {Width}x{Height}", x, y, width, height);

        return new CapturedRegion(region, stream.ToArray(), width, height);
    }
}
