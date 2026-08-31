namespace ScreenTranslator.Application;

/// <summary>Thrown by <see cref="Abstractions.IOcrService"/> implementations when recognition itself fails.</summary>
public sealed class OcrException(string message, Exception? inner = null) : Exception(message, inner);
