namespace ScreenTranslator.Domain;

/// <summary>Known failure scenarios the pipeline must be able to report to the UI.</summary>
public enum ScreenTranslatorErrorCode
{
    None,
    SelectionCancelled,
    EmptyCapture,
    NoTextFound,
    OcrFailed,
    ConnectionFailed,
    TranslationServiceUnavailable,
    UnsupportedLanguage,
    ApiLimitReached,
}

/// <summary>
/// Explicit success/failure result so pipeline steps don't rely on exceptions for
/// expected outcomes (e.g. "no text found" is a normal result, not an exception).
/// </summary>
public sealed record OperationResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public ScreenTranslatorErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }

    private OperationResult(bool success, T? value, ScreenTranslatorErrorCode errorCode, string? errorMessage)
    {
        Success = success;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static OperationResult<T> Ok(T value) => new(true, value, ScreenTranslatorErrorCode.None, null);

    public static OperationResult<T> Fail(ScreenTranslatorErrorCode errorCode, string errorMessage) =>
        new(false, default, errorCode, errorMessage);
}
