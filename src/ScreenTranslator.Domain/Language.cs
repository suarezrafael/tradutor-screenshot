namespace ScreenTranslator.Domain;

/// <summary>
/// A language identified by a BCP-47-ish code. Modeled as data (not an enum) so new
/// languages can be added by extending the registry below without touching every
/// switch statement that consumes a language type.
/// </summary>
public sealed record Language(string Code, string DisplayName)
{
    /// <summary>Pseudo-language meaning "detect the source language automatically".</summary>
    public static readonly Language AutoDetect = new("auto", "Detectar automaticamente");

    public static readonly Language English = new("en", "Inglês");
    public static readonly Language Spanish = new("es", "Espanhol");
    public static readonly Language ChineseSimplified = new("zh-Hans", "Chinês simplificado");
    public static readonly Language PortugueseBrazil = new("pt-BR", "Português (Brasil)");

    public bool IsAutoDetect => Code == AutoDetect.Code;

    /// <summary>All languages the app can currently pick as OCR/translation source.</summary>
    public static IReadOnlyList<Language> SupportedSourceLanguages { get; } =
        [AutoDetect, English, Spanish, ChineseSimplified];

    /// <summary>All languages the app can currently translate into.</summary>
    public static IReadOnlyList<Language> SupportedTargetLanguages { get; } =
        [PortugueseBrazil, English, Spanish, ChineseSimplified];

    /// <summary>Default target language on first run, per product requirements.</summary>
    public static Language DefaultTarget => PortugueseBrazil;

    public static Language? FromCode(string code) =>
        SupportedSourceLanguages.Concat(SupportedTargetLanguages)
            .FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => DisplayName;
}
