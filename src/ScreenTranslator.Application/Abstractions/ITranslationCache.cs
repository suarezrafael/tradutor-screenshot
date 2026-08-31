namespace ScreenTranslator.Application.Abstractions;

/// <summary>
/// Avoids re-translating text that was already translated recently, keyed by
/// (source language, target language, text).
/// </summary>
public interface ITranslationCache
{
    bool TryGet(string sourceLanguageCode, string targetLanguageCode, string text, out string translatedText);

    void Set(string sourceLanguageCode, string targetLanguageCode, string text, string translatedText);
}
