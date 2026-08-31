using ScreenTranslator.Domain;

namespace ScreenTranslator.Infrastructure.Translation;

/// <summary>Maps our <see cref="Language"/> registry to the ISO codes GTranslate's engines expect.</summary>
internal static class GTranslateLanguageMap
{
    public static string ToGTranslateCode(Language language) => language.Code switch
    {
        "zh-Hans" => "zh-CN",
        "pt-BR" => "pt",
        var code => code,
    };
}
