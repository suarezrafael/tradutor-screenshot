using GTranslate;
using GTranslate.Translators;
using Microsoft.Extensions.Logging;
using ScreenTranslator.Application;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;
using Language = ScreenTranslator.Domain.Language;

namespace ScreenTranslator.Infrastructure.Translation;

/// <summary>
/// Default <see cref="ITranslationService"/>: uses the <a href="https://github.com/d4n3436/GTranslate">GTranslate</a>
/// library's <see cref="AggregateTranslator"/>, which tries several key-less, free translation engines
/// in order (Google's web endpoint, Google's RPC endpoint, Microsoft/Bing, Yandex) and falls back to
/// the next one whenever one fails or is rate-limited, instead of hard-failing the whole capture the
/// moment a single free endpoint throttles a shared/datacenter IP. This mirrors the provider-fallback
/// approach used by the open-source OverTranslate project (github.com/asd880921/OverTranslate), which
/// hits the exact same "one free endpoint isn't reliable enough on its own" problem.
///
/// Because callers only see <see cref="ITranslationService"/>, swapping this out for a paid/official
/// provider (Azure Translator, DeepL, OpenAI) later is still a one-class change.
/// </summary>
public sealed class GTranslateService : ITranslationService, IDisposable
{
    private readonly AggregateTranslator _translator = new();
    private readonly ILogger<GTranslateService> _logger;

    public GTranslateService(ILogger<GTranslateService> logger)
    {
        _logger = logger;
    }

    public async Task<string> TranslateAsync(
        string text, Language sourceLanguage, Language targetLanguage, CancellationToken cancellationToken = default)
    {
        var fromCode = sourceLanguage.IsAutoDetect ? null : GTranslateLanguageMap.ToGTranslateCode(sourceLanguage);
        var toCode = GTranslateLanguageMap.ToGTranslateCode(targetLanguage);

        try
        {
            var result = await _translator.TranslateAsync(text, toCode, fromCode);
            return result.Translation;
        }
        catch (TranslatorException ex)
        {
            throw new TranslationException(
                ScreenTranslatorErrorCode.UnsupportedLanguage,
                $"Idioma não suportado pelos serviços de tradução ({sourceLanguage.DisplayName} → {targetLanguage.DisplayName}).",
                ex);
        }
        catch (AggregateException ex)
        {
            _logger.LogError(ex, "All free translation engines failed for text of length {Length}", text.Length);
            throw new TranslationException(
                ScreenTranslatorErrorCode.TranslationServiceUnavailable,
                "Não foi possível realizar a tradução. Verifique sua conexão ou tente novamente em instantes.",
                ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected translation failure");
            throw new TranslationException(
                ScreenTranslatorErrorCode.ConnectionFailed,
                "Não foi possível realizar a tradução. Verifique sua conexão.",
                ex);
        }
    }

    // The interface's default TranslateManyAsync awaits one phrase at a time, so a capture with N
    // phrases takes N sequential round-trips. OverTranslate (github.com/asd880921/OverTranslate)
    // fires every block concurrently instead, turning per-capture latency into roughly the slowest
    // single request rather than their sum - the difference between ~1-2s and 15-20s+ for a capture
    // with a few dozen phrases (a dense grid of UI buttons, say).
    public async Task<IReadOnlyList<string>> TranslateManyAsync(
        IReadOnlyList<string> texts, Language sourceLanguage, Language targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
        {
            return [];
        }

        var tasks = texts.Select(text => TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    public void Dispose() => _translator.Dispose();
}
