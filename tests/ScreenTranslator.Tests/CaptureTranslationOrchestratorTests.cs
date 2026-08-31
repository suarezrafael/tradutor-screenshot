using Microsoft.Extensions.Logging.Abstractions;
using ScreenTranslator.Application;
using ScreenTranslator.Domain;
using ScreenTranslator.Tests.Fakes;
using Xunit;

namespace ScreenTranslator.Tests;

public class CaptureTranslationOrchestratorTests
{
    private static CaptureTranslationOrchestrator BuildOrchestrator(
        IReadOnlyList<ScreenTranslator.Domain.OcrWord>? words = null,
        Func<string, string>? translate = null)
    {
        return new CaptureTranslationOrchestrator(
            new FakeScreenCaptureService(),
            new FakeOcrService(words),
            new FakeLanguageDetectionService(),
            new FakeTranslationService(translate),
            new FakeTranslationCache(),
            new TranslationOverlayService(),
            new PhraseGroupingService(),
            NullLogger<CaptureTranslationOrchestrator>.Instance);
    }

    [Fact]
    public async Task TranslateRegionAsync_FailsWithEmptyCapture_WhenRegionHasNoArea()
    {
        var orchestrator = BuildOrchestrator();

        var result = await orchestrator.TranslateRegionAsync(
            new BoundingBox(0, 0, 0, 0), Language.English, Language.PortugueseBrazil, minOverlayFontSize: 14);

        Assert.False(result.Success);
        Assert.Equal(ScreenTranslatorErrorCode.EmptyCapture, result.ErrorCode);
    }

    [Fact]
    public async Task TranslateRegionAsync_FailsWithNoTextFound_WhenOcrReturnsNoWords()
    {
        var orchestrator = BuildOrchestrator(words: []);

        var result = await orchestrator.TranslateRegionAsync(
            new BoundingBox(0, 0, 100, 50), Language.English, Language.PortugueseBrazil, minOverlayFontSize: 14);

        Assert.False(result.Success);
        Assert.Equal(ScreenTranslatorErrorCode.NoTextFound, result.ErrorCode);
    }

    [Fact]
    public async Task TranslateRegionAsync_ReturnsPositionedTranslations_ForRecognizedWords()
    {
        var words = new[]
        {
            new ScreenTranslator.Domain.OcrWord("Hello", new BoundingBox(0, 50, 40, 16), 0.9),
            new ScreenTranslator.Domain.OcrWord("world", new BoundingBox(44, 50, 40, 16), 0.9),
        };
        var orchestrator = BuildOrchestrator(words, translate: _ => "Olá mundo");

        var result = await orchestrator.TranslateRegionAsync(
            new BoundingBox(0, 0, 200, 100), Language.English, Language.PortugueseBrazil, minOverlayFontSize: 14);

        Assert.True(result.Success);
        var block = Assert.Single(result.Value!.Translations);
        Assert.Equal("Hello world", block.OriginalText);
        Assert.Equal("Olá mundo", block.TranslatedText);
    }

    [Fact]
    public async Task TranslateRegionAsync_DiscardsLowConfidencePhrases_ButKeepsHighConfidenceOnes()
    {
        // A low-confidence phrase is usually a misread that merged an icon or a neighboring button
        // into one garbled, wrongly-sized block - worse to show than to just leave untranslated.
        var words = new[]
        {
            new ScreenTranslator.Domain.OcrWord("Good", new BoundingBox(0, 0, 40, 16), 0.9),
            new ScreenTranslator.Domain.OcrWord("Bad", new BoundingBox(0, 100, 40, 16), 0.2),
        };
        var orchestrator = BuildOrchestrator(words, translate: t => t == "Good" ? "Bom" : "Ruim");

        var result = await orchestrator.TranslateRegionAsync(
            new BoundingBox(0, 0, 100, 200), Language.English, Language.PortugueseBrazil, minOverlayFontSize: 14);

        Assert.True(result.Success);
        var block = Assert.Single(result.Value!.Translations);
        Assert.Equal("Good", block.OriginalText);
    }

    [Fact]
    public async Task TranslateRegionAsync_FailsWithNoTextFound_WhenEveryPhraseIsLowConfidence()
    {
        var words = new[] { new ScreenTranslator.Domain.OcrWord("Ch Jr", new BoundingBox(0, 0, 40, 16), 0.3) };
        var orchestrator = BuildOrchestrator(words);

        var result = await orchestrator.TranslateRegionAsync(
            new BoundingBox(0, 0, 100, 50), Language.English, Language.PortugueseBrazil, minOverlayFontSize: 14);

        Assert.False(result.Success);
        Assert.Equal(ScreenTranslatorErrorCode.NoTextFound, result.ErrorCode);
    }

    [Fact]
    public async Task TranslateRegionAsync_MapsTranslationExceptionToItsErrorCode()
    {
        var words = new[] { new ScreenTranslator.Domain.OcrWord("Hi", new BoundingBox(0, 0, 20, 16), 0.9) };
        var orchestrator = new CaptureTranslationOrchestrator(
            new FakeScreenCaptureService(),
            new FakeOcrService(words),
            new FakeLanguageDetectionService(),
            new ThrowingTranslationService(ScreenTranslatorErrorCode.ApiLimitReached, "limite atingido"),
            new FakeTranslationCache(),
            new TranslationOverlayService(),
            new PhraseGroupingService(),
            NullLogger<CaptureTranslationOrchestrator>.Instance);

        var result = await orchestrator.TranslateRegionAsync(
            new BoundingBox(0, 0, 100, 50), Language.English, Language.PortugueseBrazil, minOverlayFontSize: 14);

        Assert.False(result.Success);
        Assert.Equal(ScreenTranslatorErrorCode.ApiLimitReached, result.ErrorCode);
    }

    private sealed class ThrowingTranslationService(ScreenTranslatorErrorCode code, string message)
        : ScreenTranslator.Application.Abstractions.ITranslationService
    {
        public Task<string> TranslateAsync(
            string text, Language sourceLanguage, Language targetLanguage, CancellationToken cancellationToken = default) =>
            throw new TranslationException(code, message);
    }
}
