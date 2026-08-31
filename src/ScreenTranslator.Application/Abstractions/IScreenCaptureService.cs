using ScreenTranslator.Domain;

namespace ScreenTranslator.Application.Abstractions;

/// <summary>Captures pixels from the physical screen(s). Implemented in Infrastructure via Win32/GDI.</summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Bounding box of the whole virtual desktop (union of all monitors), in physical pixels.
    /// Used by the selection overlay to know how far it can span across monitors.
    /// </summary>
    BoundingBox GetVirtualScreenBounds();

    /// <summary>Captures exactly the given region (physical pixel coordinates) of the virtual desktop.</summary>
    Task<CapturedRegion> CaptureRegionAsync(BoundingBox region, CancellationToken cancellationToken = default);
}
