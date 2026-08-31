using ScreenTranslator.Domain;
using Xunit;

namespace ScreenTranslator.Tests;

public class LanguageTests
{
    [Fact]
    public void DefaultTarget_IsPortugueseBrazil()
    {
        Assert.Equal(Language.PortugueseBrazil, Language.DefaultTarget);
    }

    [Fact]
    public void SupportedSourceLanguages_IncludesAutoDetectAndTheThreeMvpLanguages()
    {
        var codes = Language.SupportedSourceLanguages.Select(l => l.Code).ToList();

        Assert.Contains("auto", codes);
        Assert.Contains("en", codes);
        Assert.Contains("es", codes);
        Assert.Contains("zh-Hans", codes);
    }

    [Fact]
    public void SupportedTargetLanguages_DoesNotIncludeAutoDetect()
    {
        Assert.DoesNotContain(Language.SupportedTargetLanguages, l => l.IsAutoDetect);
    }

    [Fact]
    public void FromCode_IsCaseInsensitiveAndReturnsKnownLanguage()
    {
        var language = Language.FromCode("EN");

        Assert.Equal(Language.English, language);
    }

    [Fact]
    public void FromCode_ReturnsNull_ForUnknownCode()
    {
        Assert.Null(Language.FromCode("xx-unknown"));
    }
}
