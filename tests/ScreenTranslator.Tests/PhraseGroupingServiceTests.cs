using ScreenTranslator.Application;
using ScreenTranslator.Domain;
using Xunit;

namespace ScreenTranslator.Tests;

public class PhraseGroupingServiceTests
{
    private readonly PhraseGroupingService _sut = new();

    [Fact]
    public void Group_WithNoWords_ReturnsEmpty()
    {
        var result = _sut.Group([]);

        Assert.Empty(result);
    }

    [Fact]
    public void Group_MergesAdjacentWordsOnSameLineIntoOnePhrase()
    {
        // "Hello, my name is John." laid out left-to-right on one line.
        var words = new[]
        {
            new OcrWord("Hello,", new BoundingBox(0, 0, 40, 12), 0.95),
            new OcrWord("my", new BoundingBox(44, 0, 20, 12), 0.95),
            new OcrWord("name", new BoundingBox(68, 0, 35, 12), 0.95),
            new OcrWord("is", new BoundingBox(107, 0, 15, 12), 0.95),
            new OcrWord("John.", new BoundingBox(126, 0, 35, 12), 0.95),
        };

        var blocks = _sut.Group(words);

        var block = Assert.Single(blocks);
        Assert.Equal("Hello, my name is John.", block.Text);
        Assert.Equal(5, block.Words.Count);
    }

    [Fact]
    public void Group_SplitsWordsOnDifferentLinesIntoDifferentPhrases()
    {
        var words = new[]
        {
            new OcrWord("First", new BoundingBox(0, 0, 30, 12), 0.9),
            new OcrWord("line", new BoundingBox(34, 0, 25, 12), 0.9),
            new OcrWord("Second", new BoundingBox(0, 30, 40, 12), 0.9),
            new OcrWord("line", new BoundingBox(44, 30, 25, 12), 0.9),
        };

        var blocks = _sut.Group(words);

        Assert.Equal(2, blocks.Count);
        Assert.Equal("First line", blocks[0].Text);
        Assert.Equal("Second line", blocks[1].Text);
    }

    [Fact]
    public void Group_SplitsFarApartWordsOnSameLineIntoDifferentPhrases()
    {
        // Two unrelated columns of text that happen to sit at the same vertical position.
        var words = new[]
        {
            new OcrWord("Left", new BoundingBox(0, 0, 30, 12), 0.9),
            new OcrWord("column", new BoundingBox(34, 0, 40, 12), 0.9),
            new OcrWord("Right", new BoundingBox(500, 0, 30, 12), 0.9),
            new OcrWord("column", new BoundingBox(534, 0, 40, 12), 0.9),
        };

        var blocks = _sut.Group(words);

        Assert.Equal(2, blocks.Count);
        Assert.Equal("Left column", blocks[0].Text);
        Assert.Equal("Right column", blocks[1].Text);
    }

    [Fact]
    public void Group_ComputesUnionBoundingBoxForPhrase()
    {
        var words = new[]
        {
            new OcrWord("Hi", new BoundingBox(10, 20, 20, 10), 0.9),
            new OcrWord("there", new BoundingBox(34, 22, 30, 12), 0.9),
        };

        var block = Assert.Single(_sut.Group(words));

        Assert.Equal(10, block.Bounds.X);
        Assert.Equal(20, block.Bounds.Y);
        Assert.Equal(64, block.Bounds.Right);
        Assert.Equal(34, block.Bounds.Bottom);
    }

    [Fact]
    public void Group_DoesNotLetATallOutlierWordMergeTwoUnrelatedLines()
    {
        // A UI icon OCR'd as a single tall "word" spans both rows below. Without cross-checking
        // against every member of the line, its inflated bounding box would bridge the heading
        // ("Shortcuts") into the unrelated description line ("Press this shortcut to") below it.
        var words = new[]
        {
            new OcrWord("[icon]", new BoundingBox(0, 0, 20, 40), 0.9),
            new OcrWord("Shortcuts", new BoundingBox(30, 5, 80, 14), 0.9),
            new OcrWord("Press", new BoundingBox(30, 32, 120, 14), 0.9),
        };

        var blocks = _sut.Group(words);

        Assert.Equal(2, blocks.Count);
        Assert.Equal("[icon] Shortcuts", blocks[0].Text);
        Assert.Equal("Press", blocks[1].Text);
    }

    [Fact]
    public void Group_PrefersEngineProvidedLineId_OverMisleadingWordGeometry()
    {
        // Reproduces a real Tesseract quirk: "Press"/"this"/"to" are reported with an inflated
        // height (31px, vs. their neighbor "shortcut"'s normal 11px) that geometrically bridges
        // both into the heading above ("Capture") and the wrapped line below ("start ..."). The
        // engine's own per-word LineId (from its more reliable line segmentation) isn't fooled by
        // this and keeps the three lines separate.
        var words = new[]
        {
            new OcrWord("Capture", new BoundingBox(126, 180, 56, 15), 0.95, LineId: 0),
            new OcrWord("Press", new BoundingBox(126, 192, 30, 31), 0.53, LineId: 1),
            new OcrWord("this", new BoundingBox(161, 192, 20, 31), 0.96, LineId: 1),
            new OcrWord("shortcut", new BoundingBox(186, 202, 66, 11), 0.95, LineId: 1),
            new OcrWord("to", new BoundingBox(240, 192, 16, 31), 0.74, LineId: 1),
            new OcrWord("start", new BoundingBox(126, 223, 27, 9), 0.81, LineId: 2),
        };

        var blocks = _sut.Group(words);

        Assert.Equal(3, blocks.Count);
        Assert.Equal("Capture", blocks[0].Text);
        Assert.Equal("Press this shortcut to", blocks[1].Text);
        Assert.Equal("start", blocks[2].Text);
    }

    [Fact]
    public void Group_IgnoresWhitespaceOnlyOrEmptyWords()
    {
        var words = new[]
        {
            new OcrWord("Hello", new BoundingBox(0, 0, 30, 12), 0.9),
            new OcrWord("   ", new BoundingBox(34, 0, 10, 12), 0.9),
            new OcrWord("", new BoundingBox(48, 0, 5, 12), 0.9),
        };

        var block = Assert.Single(_sut.Group(words));

        Assert.Equal("Hello", block.Text);
        Assert.Single(block.Words);
    }
}
