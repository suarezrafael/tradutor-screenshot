using ScreenTranslator.Domain;

namespace ScreenTranslator.Application;

/// <summary>Thrown by <see cref="Abstractions.ITranslationService"/> implementations for known failure categories.</summary>
public sealed class TranslationException(ScreenTranslatorErrorCode errorCode, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public ScreenTranslatorErrorCode ErrorCode { get; } = errorCode;
}
