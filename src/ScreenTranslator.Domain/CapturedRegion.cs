namespace ScreenTranslator.Domain;

/// <summary>
/// A screenshot of one screen region. <see cref="ImageBytes"/> is PNG-encoded so the
/// Domain and Application layers never need to depend on a platform imaging type
/// (e.g. System.Drawing.Bitmap) — only Infrastructure/App deal with real bitmaps.
/// </summary>
public sealed record CapturedRegion(
    BoundingBox ScreenBounds,
    byte[] ImageBytes,
    int PixelWidth,
    int PixelHeight);
