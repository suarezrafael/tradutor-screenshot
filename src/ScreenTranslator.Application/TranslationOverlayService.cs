using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Application;

public sealed class TranslationOverlayService : ITranslationOverlayService
{
    private const double ProportionalFactor = 0.6;
    private const double MaxFontSize = 34.0;

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
                    FontSize: FontSizeFor(block.Bounds.Height, minFontSize)))
            .ToList();

        return _calculator.Compute(items);
    }

    // A translation for a big heading should render big, and one for small dense text should
    // render small - matching the original instead of every label using the same fixed size.
    private static double FontSizeFor(double originalHeight, double minFontSize) =>
        Math.Clamp(originalHeight * ProportionalFactor, minFontSize, MaxFontSize);
}
