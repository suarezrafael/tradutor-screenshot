namespace ScreenTranslator.Domain;

/// <summary>
/// A single word/token as recognized by the OCR engine, before phrase grouping.
/// Coordinates are in pixels relative to the captured region's image.
/// </summary>
/// <param name="LineId">
/// The OCR engine's own text-line index for this word, when the engine exposes one (-1 otherwise).
/// Individual word bounding boxes can be noisy (a word's reported height can be wildly off even
/// though the engine's own line segmentation is accurate), so phrase grouping prefers this over
/// reconstructing lines from word geometry whenever every word in the batch supplies it.
/// </param>
public sealed record OcrWord(string Text, BoundingBox Bounds, double Confidence, int LineId = -1);
