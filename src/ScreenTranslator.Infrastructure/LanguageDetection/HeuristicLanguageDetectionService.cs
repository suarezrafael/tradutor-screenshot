using System.Text.RegularExpressions;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Domain;

namespace ScreenTranslator.Infrastructure.LanguageDetection;

/// <summary>
/// Free, offline, "good enough for the MVP" language detector: Chinese is identified by the
/// presence of CJK ideographs, and English vs. Spanish is decided by counting common stopwords.
/// Swappable later for a real detection library/model without touching any caller.
/// </summary>
public sealed partial class HeuristicLanguageDetectionService : ILanguageDetectionService
{
    private static readonly HashSet<string> EnglishStopwords =
        new(StringComparer.OrdinalIgnoreCase) { "the", "is", "are", "and", "you", "your", "this", "that", "with", "have", "for" };

    private static readonly HashSet<string> SpanishStopwords =
        new(StringComparer.OrdinalIgnoreCase) { "el", "la", "los", "las", "de", "que", "es", "y", "un", "una", "para", "con" };

    public Task<Language> DetectLanguageAsync(string text, CancellationToken cancellationToken = default)
    {
        if (ChineseRegex().IsMatch(text))
        {
            return Task.FromResult(Language.ChineseSimplified);
        }

        var words = WordRegex().Matches(text).Select(m => m.Value);

        var englishScore = 0;
        var spanishScore = 0;

        foreach (var word in words)
        {
            if (EnglishStopwords.Contains(word)) englishScore++;
            if (SpanishStopwords.Contains(word)) spanishScore++;
        }

        var detected = spanishScore > englishScore ? Language.Spanish : Language.English;
        return Task.FromResult(detected);
    }

    [GeneratedRegex(@"\p{IsCJKUnifiedIdeographs}")]
    private static partial Regex ChineseRegex();

    [GeneratedRegex(@"[\p{L}]+")]
    private static partial Regex WordRegex();
}
