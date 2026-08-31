using ScreenTranslator.Application;
using Xunit;

namespace ScreenTranslator.Tests;

public class MemoryTranslationCacheTests
{
    [Fact]
    public void TryGet_ReturnsFalse_WhenNothingWasCached()
    {
        var cache = new MemoryTranslationCache();

        var found = cache.TryGet("en", "pt-BR", "Hello", out var translated);

        Assert.False(found);
        Assert.Equal(string.Empty, translated);
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsTheCachedTranslation()
    {
        var cache = new MemoryTranslationCache();

        cache.Set("en", "pt-BR", "Hello", "Olá");
        var found = cache.TryGet("en", "pt-BR", "Hello", out var translated);

        Assert.True(found);
        Assert.Equal("Olá", translated);
    }

    [Fact]
    public void TryGet_IsScopedByLanguagePair_NotJustText()
    {
        var cache = new MemoryTranslationCache();

        cache.Set("en", "pt-BR", "Hello", "Olá");
        var foundForDifferentTarget = cache.TryGet("en", "es", "Hello", out _);

        Assert.False(foundForDifferentTarget);
    }

    [Fact]
    public void TryGet_ReturnsFalse_AfterEntryExpires()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var cache = new MemoryTranslationCache(TimeSpan.FromMinutes(10), () => now);

        cache.Set("en", "pt-BR", "Hello", "Olá");
        now = now.AddMinutes(11);
        var found = cache.TryGet("en", "pt-BR", "Hello", out _);

        Assert.False(found);
    }

    [Fact]
    public void BuildKey_IsStableForSameInputs()
    {
        var key1 = MemoryTranslationCache.BuildKey("en", "pt-BR", "Hello");
        var key2 = MemoryTranslationCache.BuildKey("en", "pt-BR", "Hello");

        Assert.Equal(key1, key2);
    }
}
