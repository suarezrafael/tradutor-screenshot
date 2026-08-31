using ScreenTranslator.Domain;

namespace ScreenTranslator.Application.Abstractions;

/// <summary>
/// Translates text. Kept provider-agnostic on purpose: the default implementation calls a free
/// Google Translate endpoint, but Azure/DeepL/OpenAI/local-model implementations can be swapped
/// in later without touching any caller of this interface.
/// </summary>
public interface ITranslationService
{
    Task<string> TranslateAsync(
        string text,
        Language sourceLanguage,
        Language targetLanguage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates multiple phrases in as few round-trips as the provider allows, instead of one
    /// call per phrase. Default implementation just loops <see cref="TranslateAsync"/>; providers
    /// that support real batching should override it.
    /// </summary>
    async Task<IReadOnlyList<string>> TranslateManyAsync(
        IReadOnlyList<string> texts,
        Language sourceLanguage,
        Language targetLanguage,
        CancellationToken cancellationToken = default)
    {
        var results = new List<string>(texts.Count);
        foreach (var text in texts)
        {
            results.Add(await TranslateAsync(text, sourceLanguage, targetLanguage, cancellationToken));
        }
        return results;
    }
}
