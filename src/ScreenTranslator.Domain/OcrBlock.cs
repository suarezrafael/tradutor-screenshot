namespace ScreenTranslator.Domain;

/// <summary>
/// A phrase-level OCR result: one or more <see cref="OcrWord"/>s that were grouped
/// together (same line / same sentence) so translation happens per-phrase instead of
/// per-word. <see cref="Bounds"/> is the union of all constituent words' bounds.
/// </summary>
public sealed record OcrBlock(
    string Text,
    BoundingBox Bounds,
    double Confidence,
    IReadOnlyList<OcrWord> Words,
    string? DetectedLanguage = null);
