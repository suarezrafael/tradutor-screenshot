using ScreenTranslator.Domain;
using Xunit;

namespace ScreenTranslator.Tests;

public class BoundingBoxTests
{
    [Fact]
    public void RightAndBottom_AreComputedFromOriginAndSize()
    {
        var box = new BoundingBox(10, 20, 100, 50);

        Assert.Equal(110, box.Right);
        Assert.Equal(70, box.Bottom);
    }

    [Theory]
    [InlineData(0, 0, 10, 10, 5, 5, 10, 10, true)]   // overlapping
    [InlineData(0, 0, 10, 10, 20, 20, 10, 10, false)] // disjoint
    [InlineData(0, 0, 10, 10, 10, 0, 10, 10, false)]  // touching edges only, no area overlap
    public void IntersectsWith_DetectsOverlapCorrectly(
        double x1, double y1, double w1, double h1,
        double x2, double y2, double w2, double h2,
        bool expected)
    {
        var a = new BoundingBox(x1, y1, w1, h1);
        var b = new BoundingBox(x2, y2, w2, h2);

        Assert.Equal(expected, a.IntersectsWith(b));
        Assert.Equal(expected, b.IntersectsWith(a));
    }

    [Fact]
    public void Union_ReturnsSmallestBoxContainingBoth()
    {
        var a = new BoundingBox(0, 0, 10, 10);
        var b = new BoundingBox(5, 5, 20, 5);

        var union = a.Union(b);

        Assert.Equal(0, union.X);
        Assert.Equal(0, union.Y);
        Assert.Equal(25, union.Right);
        Assert.Equal(10, union.Bottom);
    }
}
