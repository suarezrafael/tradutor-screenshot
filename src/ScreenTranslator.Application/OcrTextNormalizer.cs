using System.Text.RegularExpressions;

namespace ScreenTranslator.Application;

/// <summary>Cleans up raw OCR text before it is grouped/translated.</summary>
public static partial class OcrTextNormalizer
{
    /// <summary>Trims and collapses runs of whitespace produced by OCR engines into single spaces.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(text.Trim(), " ");
    }

    /// <summary>True when, after normalization, the text has at least one letter or digit worth translating.</summary>
    public static bool IsMeaningful(string? text)
    {
        var normalized = Normalize(text);
        return normalized.Length > 0 && normalized.Any(char.IsLetterOrDigit);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
