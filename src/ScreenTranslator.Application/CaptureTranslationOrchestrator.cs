using Microsoft.Extensions.Logging;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Application;

/// <summary>Result of a full capture-to-overlay run: the screenshot plus its positioned translations.</summary>
public sealed record TranslationSessionResult(CapturedRegion Capture, IReadOnlyList<TranslationBlock> Translations);

/// <summary>
/// Coordinates the full pipeline described in the product spec:
/// capture -> OCR -> phrase grouping -> language detection -> translation (cached) -> overlay layout.
/// This is the only class that knows the order of those steps; every step itself stays independently testable.
/// </summary>
public sealed class CaptureTranslationOrchestrator(
    IScreenCaptureService captureService,
    IOcrService ocrService,
    ILanguageDetectionService languageDetectionService,
    ITranslationService translationService,
    ITranslationCache translationCache,
    ITranslationOverlayService overlayService,
    PhraseGroupingService phraseGrouping,
    ILogger<CaptureTranslationOrchestrator> logger)
{
    /// <summary>
    /// Grabs the region's pixels only - split out from <see cref="TranslateRegionAsync"/> so a caller
    /// (see CaptureFlowController) can show a "processing" indicator only *after* the screenshot is
    /// safely taken. Showing it any earlier risks the indicator itself ending up in frame if it
    /// overlaps the selected region.
    /// </summary>
    public async Task<OperationResult<CapturedRegion>> CaptureAsync(
        BoundingBox region, CancellationToken cancellationToken = default)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            logger.LogWarning("Capture requested with an empty region {Region}", region);
            return OperationResult<CapturedRegion>.Fail(
                ScreenTranslatorErrorCode.EmptyCapture, "Nenhuma área foi selecionada.");
        }

        var capturedRegion = await captureService.CaptureRegionAsync(region, cancellationToken);
        logger.LogInformation("Captured {Width}x{Height} region at ({X},{Y})",
            capturedRegion.PixelWidth, capturedRegion.PixelHeight, region.X, region.Y);

        return OperationResult<CapturedRegion>.Ok(capturedRegion);
    }

    public async Task<OperationResult<TranslationSessionResult>> TranslateRegionAsync(
        BoundingBox region,
        Language sourceLanguage,
        Language targetLanguage,
        double minOverlayFontSize,
        CancellationToken cancellationToken = default)
    {
        var captureResult = await CaptureAsync(region, cancellationToken);
        if (!captureResult.Success)
        {
            return OperationResult<TranslationSessionResult>.Fail(captureResult.ErrorCode, captureResult.ErrorMessage!);
        }

        return await TranslateCapturedRegionAsync(
            captureResult.Value!, sourceLanguage, targetLanguage, minOverlayFontSize, cancellationToken);
    }

    public async Task<OperationResult<TranslationSessionResult>> TranslateCapturedRegionAsync(
        CapturedRegion capturedRegion,
        Language sourceLanguage,
        Language targetLanguage,
        double minOverlayFontSize,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<OcrWord> words;
        try
        {
            words = await ocrService.RecognizeAsync(capturedRegion, sourceLanguage, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OCR failed for captured region");
            return OperationResult<TranslationSessionResult>.Fail(
                ScreenTranslatorErrorCode.OcrFailed, "Não foi possível reconhecer o texto da imagem.");
        }

        var blocks = phraseGrouping.Group(words);
        logger.LogInformation("OCR found {WordCount} words grouped into {BlockCount} phrases", words.Count, blocks.Count);

        if (blocks.Count == 0)
        {
            return OperationResult<TranslationSessionResult>.Fail(
                ScreenTranslatorErrorCode.NoTextFound, "Nenhum texto foi encontrado nesta região.");
        }

        var resolvedSourceLanguage = sourceLanguage;
        if (sourceLanguage.IsAutoDetect)
        {
            var sampleText = string.Join('\n', blocks.Select(b => b.Text));
            resolvedSourceLanguage = await languageDetectionService.DetectLanguageAsync(sampleText, cancellationToken);
            logger.LogInformation("Auto-detected source language: {Language}", resolvedSourceLanguage.Code);
        }

        IReadOnlyList<string> translatedTexts;
        try
        {
            translatedTexts = await TranslateWithCacheAsync(blocks, resolvedSourceLanguage, targetLanguage, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TranslationException ex)
        {
            logger.LogError(ex, "Translation failed with known error code {ErrorCode}", ex.ErrorCode);
            return OperationResult<TranslationSessionResult>.Fail(ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Translation failed unexpectedly");
            return OperationResult<TranslationSessionResult>.Fail(
                ScreenTranslatorErrorCode.ConnectionFailed,
                "Não foi possível realizar a tradução. Verifique sua conexão.");
        }

        var overlay = overlayService.ComputeOverlay(blocks, translatedTexts, minOverlayFontSize);

        return OperationResult<TranslationSessionResult>.Ok(new TranslationSessionResult(capturedRegion, overlay));
    }

    private async Task<IReadOnlyList<string>> TranslateWithCacheAsync(
        IReadOnlyList<OcrBlock> blocks,
        Language sourceLanguage,
        Language targetLanguage,
        CancellationToken cancellationToken)
    {
        var results = new string?[blocks.Count];
        var pendingIndexes = new List<int>();
        var pendingTexts = new List<string>();

        for (var i = 0; i < blocks.Count; i++)
        {
            if (translationCache.TryGet(sourceLanguage.Code, targetLanguage.Code, blocks[i].Text, out var cached))
            {
                results[i] = cached;
            }
            else
            {
                pendingIndexes.Add(i);
                pendingTexts.Add(blocks[i].Text);
            }
        }

        if (pendingTexts.Count > 0)
        {
            var translated = await translationService.TranslateManyAsync(
                pendingTexts, sourceLanguage, targetLanguage, cancellationToken);

            for (var i = 0; i < pendingIndexes.Count; i++)
            {
                var blockIndex = pendingIndexes[i];
                results[blockIndex] = translated[i];
                translationCache.Set(sourceLanguage.Code, targetLanguage.Code, blocks[blockIndex].Text, translated[i]);
            }
        }

        return results!;
    }
}
