using ScreenTranslator.Application;
using Xunit;

namespace ScreenTranslator.Tests;

public class OcrTextNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Hello   world  ", "Hello world")]
    [InlineData("Hello\n\tworld", "Hello world")]
    public void Normalize_CollapsesWhitespaceAndTrims(string? input, string expected)
    {
        Assert.Equal(expected, OcrTextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("...", false)]
    [InlineData("-", false)]
    [InlineData("Hello", true)]
    [InlineData("123", true)]
    public void IsMeaningful_RequiresAtLeastOneLetterOrDigit(string? input, bool expected)
    {
        Assert.Equal(expected, OcrTextNormalizer.IsMeaningful(input));
    }
}
