using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ScreenTranslator.Application;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;
using Tesseract;

namespace ScreenTranslator.Infrastructure.Ocr;

/// <summary>
/// Local, offline, free OCR via Tesseract. Requires the trained-data files listed in
/// scripts/download-tessdata.ps1 to exist under <see cref="_tessDataPath"/>.
/// </summary>
public sealed class TesseractOcrService : IOcrService, IDisposable
{
    // Small UI text (button labels, table cells) is often below the size Tesseract reliably reads;
    // upscaling before recognition is the standard mitigation and measurably reduces misread/merged
    // words for dense screenshots. Coordinates are scaled back down before returning.
    private const float UpscaleFactor = 2.0f;

    private readonly string _tessDataPath;
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly ConcurrentDictionary<string, TesseractEngine> _engines = new();

    public TesseractOcrService(ILogger<TesseractOcrService> logger, string? tessDataPath = null)
    {
        _logger = logger;
        _tessDataPath = tessDataPath ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
    }

    public Task<IReadOnlyList<OcrWord>> RecognizeAsync(
        CapturedRegion image, Language sourceLanguage, CancellationToken cancellationToken = default) =>
        Task.Run(() => Recognize(image, sourceLanguage), cancellationToken);

    private IReadOnlyList<OcrWord> Recognize(CapturedRegion image, Language sourceLanguage)
    {
        var engine = GetOrCreateEngine(sourceLanguage);

        using var originalPix = Pix.LoadFromMemory(image.ImageBytes);
        using var pix = originalPix.Scale(UpscaleFactor, UpscaleFactor);
        // Tesseract's default automatic page segmentation (PSM Auto) is tuned for document-like
        // layouts and silently drops scattered, isolated text - exactly what a UI screenshot full of
        // small buttons/labels surrounded by borders and icons looks like to it. SparseText is built
        // for finding as much text as possible in no particular order, which is what we want here.
        using var page = engine.Process(pix, PageSegMode.SparseText);
        using var iterator = page.GetIterator();

        var words = new List<OcrWord>();
        iterator.Begin();

        // Individual word bounding boxes coming out of Tesseract can be noisy (a word's reported
        // height is sometimes wildly larger than its neighbors' on the very same line), which can
        // make geometry-only line reconstruction merge unrelated lines, and inflate any phrase's
        // union bounding box enough to throw off overlay positioning. Tesseract's own TextLine
        // segmentation (based on the line's actual pixel row projection, not per-glyph boxes) is
        // far more stable, so track it here, let PhraseGroupingService prefer it for grouping, and
        // clip each word's vertical extent to it so a single mismeasured word can't inflate its
        // phrase's bounds either.
        var lineId = -1;
        Rect lineRect = default;

        do
        {
            // Checked unconditionally, before any `continue` below, so a skipped (whitespace/no-bbox)
            // leading token doesn't cause the next real word on that line to be missed as a new line.
            if (iterator.IsAtBeginningOf(PageIteratorLevel.TextLine))
            {
                lineId++;
                iterator.TryGetBoundingBox(PageIteratorLevel.TextLine, out lineRect);
            }

            var text = iterator.GetText(PageIteratorLevel.Word);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (!iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
            {
                continue;
            }

            var confidence = iterator.GetConfidence(PageIteratorLevel.Word) / 100.0;
            var top = Math.Max(rect.Y1, lineRect.Y1);
            var bottom = Math.Min(rect.Y1 + rect.Height, lineRect.Y1 + lineRect.Height);
            var height = bottom > top ? bottom - top : rect.Height;
            var bounds = new BoundingBox(
                rect.X1 / UpscaleFactor, top / UpscaleFactor, rect.Width / UpscaleFactor, height / UpscaleFactor);
            words.Add(new OcrWord(text.Trim(), bounds, confidence, lineId));
        }
        while (iterator.Next(PageIteratorLevel.Word));

        _logger.LogInformation("Tesseract recognized {WordCount} words", words.Count);
        return words;
    }

    private TesseractEngine GetOrCreateEngine(Language language)
    {
        var tesseractLanguages = TesseractLanguageMap.ToTesseractLanguageString(language);

        return _engines.GetOrAdd(tesseractLanguages, langs =>
        {
            try
            {
                _logger.LogInformation("Loading Tesseract engine for languages '{Languages}' from {Path}", langs, _tessDataPath);
                return new TesseractEngine(_tessDataPath, langs, EngineMode.Default);
            }
            catch (Exception ex)
            {
                throw new OcrException(
                    $"Não foi possível carregar os dados de OCR para '{langs}'. " +
                    $"Execute scripts/download-tessdata.ps1 para baixar os arquivos necessários em {_tessDataPath}.",
                    ex);
            }
        });
    }

    public void Dispose()
    {
        foreach (var engine in _engines.Values)
        {
            engine.Dispose();
        }

        _engines.Clear();
    }
}
