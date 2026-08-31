namespace ScreenTranslator.Domain;

/// <summary>
/// Axis-aligned rectangle in a 2D coordinate space. Used both for physical-pixel screen
/// regions and for OCR/translation bounding boxes, always in the same coordinate system
/// as the image they were computed from.
/// </summary>
public readonly record struct BoundingBox(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public double Area => Width * Height;

    public bool IntersectsWith(BoundingBox other) =>
        Left < other.Right && Right > other.Left &&
        Top < other.Bottom && Bottom > other.Top;

    /// <summary>Smallest bounding box that contains both boxes.</summary>
    public BoundingBox Union(BoundingBox other)
    {
        var left = Math.Min(Left, other.Left);
        var top = Math.Min(Top, other.Top);
        var right = Math.Max(Right, other.Right);
        var bottom = Math.Max(Bottom, other.Bottom);
        return new BoundingBox(left, top, right - left, bottom - top);
    }

    public BoundingBox OffsetBy(double dx, double dy) => new(X + dx, Y + dy, Width, Height);

    public static readonly BoundingBox Empty = new(0, 0, 0, 0);
}
