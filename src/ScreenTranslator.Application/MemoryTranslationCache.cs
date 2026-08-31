using System.Collections.Concurrent;
using ScreenTranslator.Application.Abstractions;

namespace ScreenTranslator.Application;

/// <summary>
/// In-process translation cache keyed by (source language + target language + text), so repeatedly
/// capturing the same text (e.g. a header shown on every page of a site) doesn't cost another API call.
/// </summary>
public sealed class MemoryTranslationCache(TimeSpan? timeToLive = null, Func<DateTimeOffset>? nowProvider = null)
    : ITranslationCache
{
    private readonly TimeSpan _timeToLive = timeToLive ?? TimeSpan.FromHours(24);
    private readonly Func<DateTimeOffset> _now = nowProvider ?? (() => DateTimeOffset.UtcNow);
    private readonly ConcurrentDictionary<string, (string Translated, DateTimeOffset ExpiresAt)> _entries = new();

    public bool TryGet(string sourceLanguageCode, string targetLanguageCode, string text, out string translatedText)
    {
        var key = BuildKey(sourceLanguageCode, targetLanguageCode, text);

        if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > _now())
        {
            translatedText = entry.Translated;
            return true;
        }

        _entries.TryRemove(key, out _);
        translatedText = string.Empty;
        return false;
    }

    public void Set(string sourceLanguageCode, string targetLanguageCode, string text, string translatedText)
    {
        var key = BuildKey(sourceLanguageCode, targetLanguageCode, text);
        _entries[key] = (translatedText, _now() + _timeToLive);
    }

    public static string BuildKey(string sourceLanguageCode, string targetLanguageCode, string text) =>
        $"{sourceLanguageCode}|{targetLanguageCode}|{text}";
}
