using ScreenTranslator.Application;
using ScreenTranslator.Domain;
using Xunit;

namespace ScreenTranslator.Tests;

public class TranslationOverlayServiceTests
{
    private readonly TranslationOverlayService _sut = new();

    [Fact]
    public void ComputeOverlay_ReturnsOneTranslationBlockPerOriginalBlock()
    {
        var blocks = new[]
        {
            new OcrBlock("Hello", new BoundingBox(0, 50, 40, 16), 0.9, []),
            new OcrBlock("World", new BoundingBox(0, 80, 40, 16), 0.9, []),
        };

        var result = _sut.ComputeOverlay(blocks, ["Olá", "Mundo"], minFontSize: 14);

        Assert.Equal(2, result.Count);
        Assert.Equal("Hello", result[0].OriginalText);
        Assert.Equal("Olá", result[0].TranslatedText);
    }

    [Fact]
    public void ComputeOverlay_PlacesEachTranslationExactlyOverItsOwnOriginal()
    {
        // OverTranslate-style overlay: no adjacent-slot search, just replace the original in place.
        var blocks = new[] { new OcrBlock("Hello", new BoundingBox(10, 20, 40, 16), 0.9, []) };

        var block = Assert.Single(_sut.ComputeOverlay(blocks, ["Olá"], minFontSize: 14));

        Assert.Equal(block.OriginalBounds, block.TranslationBounds);
    }

    [Fact]
    public void ComputeOverlay_Throws_WhenBlockAndTranslationCountsDiffer()
    {
        var blocks = new[] { new OcrBlock("Hello", new BoundingBox(0, 0, 40, 16), 0.9, []) };

        Assert.Throws<ArgumentException>(() =>
            _sut.ComputeOverlay(blocks, ["Olá", "Extra"], minFontSize: 14));
    }

    [Fact]
    public void ComputeOverlay_ScalesFontSizeProportionallyToOriginalHeight()
    {
        // A translation for a big heading should render bigger than one for small dense text,
        // instead of every label using the same fixed size regardless of the original.
        var blocks = new[]
        {
            new OcrBlock("BIG HEADING", new BoundingBox(0, 0, 200, 40), 0.9, []),
            new OcrBlock("small print", new BoundingBox(0, 100, 200, 10), 0.9, []),
        };

        var result = _sut.ComputeOverlay(blocks, ["TÍTULO", "miúdo"], minFontSize: 6);

        var heading = result.Single(r => r.OriginalText == "BIG HEADING");
        var smallPrint = result.Single(r => r.OriginalText == "small print");
        Assert.True(heading.FontSize > smallPrint.FontSize);
    }

    [Fact]
    public void ComputeOverlay_NeverGoesBelowTheConfiguredMinimumFontSize()
    {
        // Original text is tiny (a dense table, say); the translation must still be readable.
        var blocks = new[] { new OcrBlock("tiny", new BoundingBox(0, 0, 30, 6), 0.9, []) };

        var result = _sut.ComputeOverlay(blocks, ["minúsculo"], minFontSize: 9);

        Assert.True(Assert.Single(result).FontSize >= 9);
    }

    [Fact]
    public void ComputeOverlay_SizesFromTheSmallerOfHeightAndWidth_WhenABadOcrReadingInflatesOneOfThem()
    {
        // A misread that merges a small button with an unrelated neighbor (see
        // PhraseGroupingServiceTests' tall-outlier-word case) inflates the block's height while its
        // width stays narrow (a short button label). Sizing from height alone would blow the label
        // up to a huge, disproportionate size; using whichever dimension is smaller keeps it sane.
        var blocks = new[] { new OcrBlock("Base Mult", new BoundingBox(0, 0, 40, 60), 0.9, []) };

        var result = _sut.ComputeOverlay(blocks, ["Base Mult"], minFontSize: 6);

        Assert.True(Assert.Single(result).FontSize <= 40 * 0.6 + 0.01);
    }
}
