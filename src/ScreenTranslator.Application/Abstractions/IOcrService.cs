using ScreenTranslator.Domain;

namespace ScreenTranslator.Application.Abstractions;

/// <summary>Runs OCR over a captured region. Implemented in Infrastructure (Tesseract).</summary>
public interface IOcrService
{
    /// <summary>
    /// Recognizes text in <paramref name="image"/>. When <paramref name="sourceLanguage"/> is
    /// <see cref="Language.AutoDetect"/>, the implementation should attempt recognition across
    /// all supported scripts (e.g. combined trained data) rather than requiring a single guess upfront.
    /// </summary>
    Task<IReadOnlyList<OcrWord>> RecognizeAsync(
        CapturedRegion image,
        Language sourceLanguage,
        CancellationToken cancellationToken = default);
}
