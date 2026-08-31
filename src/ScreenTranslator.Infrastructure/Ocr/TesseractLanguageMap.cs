using ScreenTranslator.Domain;

namespace ScreenTranslator.Infrastructure.Ocr;

/// <summary>Maps our <see cref="Language"/> registry to Tesseract's trained-data language codes.</summary>
internal static class TesseractLanguageMap
{
    /// <summary>
    /// Tesseract can recognize several scripts in a single pass when given a "+"-joined language
    /// string, which is how we implement "auto-detect": try every supported script at once instead
    /// of guessing one language up front.
    /// </summary>
    public static string ToTesseractLanguageString(Language language)
    {
        if (language.IsAutoDetect)
        {
            return string.Join('+', Language.SupportedSourceLanguages
                .Where(l => !l.IsAutoDetect)
                .Select(ToSingleTesseractCode));
        }

        return ToSingleTesseractCode(language);
    }

    private static string ToSingleTesseractCode(Language language) => language.Code switch
    {
        "en" => "eng",
        "es" => "spa",
        "zh-Hans" => "chi_sim",
        _ => throw new NotSupportedException($"No Tesseract mapping for language '{language.Code}'."),
    };
}
