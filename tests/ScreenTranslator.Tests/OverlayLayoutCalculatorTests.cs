using ScreenTranslator.Application;
using ScreenTranslator.Domain;
using Xunit;

namespace ScreenTranslator.Tests;

public class OverlayLayoutCalculatorTests
{
    private readonly OverlayLayoutCalculator _sut = new();

    [Fact]
    public void Compute_PlacesTranslationExactlyOverItsOwnOriginal()
    {
        // OverTranslate-style overlay: the label replaces the original in place instead of being
        // laid out in adjacent free space, so there's no collision-avoidance math to get wrong.
        var items = new[]
        {
            ("Hello world", "Olá mundo", new BoundingBox(10, 100, 80, 16), 12.0),
        };

        var result = _sut.Compute(items);

        var block = Assert.Single(result);
        Assert.Equal(block.OriginalBounds, block.TranslationBounds);
    }

    [Fact]
    public void Compute_LetsTwoTranslationLabelsOverlapWhenTheirOriginalsDo()
    {
        // Two originals that happen to overlap (e.g. noisy OCR bounds) simply produce two
        // overlapping labels - there's no attempt to nudge either one away from the other, since
        // each label's position is entirely determined by its own original text.
        var items = new[]
        {
            ("Line one", "Primeira linha", new BoundingBox(0, 40, 100, 20), 12.0),
            ("Line two", "Segunda linha", new BoundingBox(0, 50, 100, 20), 12.0),
        };

        var result = _sut.Compute(items);

        Assert.Equal(2, result.Count);
        Assert.Equal(new BoundingBox(0, 40, 100, 20), result[0].TranslationBounds);
        Assert.Equal(new BoundingBox(0, 50, 100, 20), result[1].TranslationBounds);
    }

    [Fact]
    public void Compute_CarriesEachItemsOwnFontSizeThroughToItsResult()
    {
        // Per-item font sizing is decided upstream (by TranslationOverlayService, proportionally to
        // each phrase's original height); this layer just carries it through unchanged.
        var items = new[]
        {
            ("BIG HEADING", "TÍTULO GRANDE", new BoundingBox(0, 100, 200, 40), 24.0),
            ("small print", "letra miúda", new BoundingBox(0, 200, 200, 10), 8.0),
        };

        var result = _sut.Compute(items);

        Assert.Equal(24.0, result.Single(r => r.OriginalText == "BIG HEADING").FontSize);
        Assert.Equal(8.0, result.Single(r => r.OriginalText == "small print").FontSize);
    }

    [Fact]
    public void Compute_PreservesInputOrder()
    {
        var items = new[]
        {
            ("Second", "Segundo", new BoundingBox(0, 100, 50, 16), 12.0),
            ("First", "Primeiro", new BoundingBox(0, 10, 50, 16), 12.0),
        };

        var result = _sut.Compute(items);

        Assert.Equal("Second", result[0].OriginalText);
        Assert.Equal("First", result[1].OriginalText);
    }
}
