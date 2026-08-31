using ScreenTranslator.Domain;

namespace ScreenTranslator.Application.Abstractions;

/// <summary>Guesses the language of a piece of recognized text when the user chose "auto-detect".</summary>
public interface ILanguageDetectionService
{
    Task<Language> DetectLanguageAsync(string text, CancellationToken cancellationToken = default);
}
