using ScreenTranslator.Domain;

namespace ScreenTranslator.Application;

/// <summary>
/// Places each translation label exactly over its own original text (same position and size),
/// like OverTranslate does, instead of finding an obstacle-free spot nearby - which sidesteps the
/// whole class of "translation drifted near unrelated content" bugs that come with that approach.
/// A translation wider than the space it's replacing gets truncated with an ellipsis and a tooltip
/// in the UI layer (see ResultWindow) rather than being laid out differently here.
/// </summary>
public sealed class OverlayLayoutCalculator
{
    public IReadOnlyList<TranslationBlock> Compute(
        IReadOnlyList<(string OriginalText, string TranslatedText, BoundingBox OriginalBounds, double FontSize)> items)
    {
        return items
            .Select(item => new TranslationBlock(
                item.OriginalText, item.TranslatedText, item.OriginalBounds, item.OriginalBounds, item.FontSize))
            .ToList();
    }
}
