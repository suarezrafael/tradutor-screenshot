using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Tests.Fakes;

public sealed class FakeScreenCaptureService(int pixelWidth = 200, int pixelHeight = 100) : IScreenCaptureService
{
    public BoundingBox GetVirtualScreenBounds() => new(0, 0, 1920, 1080);

    public Task<CapturedRegion> CaptureRegionAsync(BoundingBox region, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CapturedRegion(region, [], pixelWidth, pixelHeight));
}

public sealed class FakeOcrService(IReadOnlyList<OcrWord>? words = null) : IOcrService
{
    public Task<IReadOnlyList<OcrWord>> RecognizeAsync(
        CapturedRegion image, Language sourceLanguage, CancellationToken cancellationToken = default) =>
        Task.FromResult(words ?? []);
}

public sealed class FakeLanguageDetectionService(Language? detected = null) : ILanguageDetectionService
{
    public Task<Language> DetectLanguageAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(detected ?? Language.English);
}

public sealed class FakeTranslationService(Func<string, string>? translate = null) : ITranslationService
{
    public Task<string> TranslateAsync(
        string text, Language sourceLanguage, Language targetLanguage, CancellationToken cancellationToken = default) =>
        Task.FromResult(translate?.Invoke(text) ?? $"[{targetLanguage.Code}] {text}");
}

public sealed class FakeTranslationCache : ITranslationCache
{
    private readonly Dictionary<string, string> _entries = new();

    public bool TryGet(string sourceLanguageCode, string targetLanguageCode, string text, out string translatedText)
    {
        if (_entries.TryGetValue($"{sourceLanguageCode}|{targetLanguageCode}|{text}", out var value))
        {
            translatedText = value;
            return true;
        }

        translatedText = string.Empty;
        return false;
    }

    public void Set(string sourceLanguageCode, string targetLanguageCode, string text, string translatedText) =>
        _entries[$"{sourceLanguageCode}|{targetLanguageCode}|{text}"] = translatedText;
}
