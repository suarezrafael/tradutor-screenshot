namespace ScreenTranslator.Domain;

/// <summary>
/// One translated phrase, carrying both the original OCR bounds and the computed
/// on-screen position for the translation label (set by the overlay layout step).
/// </summary>
/// <param name="FontSize">
/// Sized proportionally to <see cref="OriginalBounds"/>' height, so a translation for a large
/// heading renders large and a translation for small body text renders small - instead of every
/// label using the same fixed size regardless of the original text it stands in for.
/// </param>
public sealed record TranslationBlock(
    string OriginalText,
    string TranslatedText,
    BoundingBox OriginalBounds,
    BoundingBox TranslationBounds,
    double FontSize);
