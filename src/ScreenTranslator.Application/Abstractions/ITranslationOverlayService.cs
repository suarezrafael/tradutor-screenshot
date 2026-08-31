using ScreenTranslator.Domain;

namespace ScreenTranslator.Application.Abstractions;

/// <summary>Builds translation labels sized for each original phrase, positioned exactly over it.</summary>
public interface ITranslationOverlayService
{
    IReadOnlyList<TranslationBlock> ComputeOverlay(
        IReadOnlyList<OcrBlock> originalBlocks,
        IReadOnlyList<string> translatedTexts,
        double minFontSize);
}
