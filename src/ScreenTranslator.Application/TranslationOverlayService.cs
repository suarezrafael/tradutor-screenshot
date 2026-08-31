using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Application;

public sealed class TranslationOverlayService : ITranslationOverlayService
{
    private const double ProportionalFactor = 0.6;
    // Deliberately modest: a legitimate heading rarely needs more than this, and it caps how far a
    // single bad OCR reading (see FontSizeFor) can blow a label out of proportion.
    private const double MaxFontSize = 20.0;

    private readonly OverlayLayoutCalculator _calculator = new();

    public IReadOnlyList<TranslationBlock> ComputeOverlay(
        IReadOnlyList<OcrBlock> originalBlocks,
        IReadOnlyList<string> translatedTexts,
        double minFontSize)
    {
        if (originalBlocks.Count != translatedTexts.Count)
        {
            throw new ArgumentException(
                $"{nameof(originalBlocks)} and {nameof(translatedTexts)} must have the same length.");
        }

        var items = originalBlocks.Zip(translatedTexts,
                (block, translated) => (
                    OriginalText: block.Text,
                    TranslatedText: translated,
                    OriginalBounds: block.Bounds,
                    FontSize: FontSizeFor(block.Bounds, minFontSize)))
            .ToList();

        return _calculator.Compute(items);
    }

    // A translation for a big heading should render big, and one for small dense text should render
    // small - matching the original instead of every label using the same fixed size. Sized from
    // whichever of height/width is smaller, not height alone: an OCR block whose bounds are
    // inflated because a misread merged it with a neighbor (see PhraseGroupingService's LineId
    // handling) is usually tall-but-narrow or wide-but-short, never inflated in both dimensions at
    // once, so the smaller one is still a reasonable proxy for the original text's true size.
    private static double FontSizeFor(BoundingBox originalBounds, double minFontSize) =>
        Math.Clamp(Math.Min(originalBounds.Height, originalBounds.Width) * ProportionalFactor, minFontSize, MaxFontSize);
}
