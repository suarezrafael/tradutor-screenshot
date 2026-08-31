using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ScreenTranslator.Desktop.Logging;

/// <summary>
/// Minimal structured file logger (one line per event: timestamp, level, category, message,
/// exception) so issues can be diagnosed from %AppData%\ScreenTranslator\logs without attaching
/// a debugger. Deliberately dependency-free instead of pulling in Serilog for an MVP.
/// </summary>
public sealed class FileLoggerProvider(string filePath) : ILoggerProvider
{
    private readonly object _writeLock = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, filePath, _writeLock);

    public void Dispose()
    {
    }

    private sealed class FileLogger(string categoryName, string filePath, object writeLock) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} " +
                       $"[{logLevel}] {categoryName}: {formatter(state, exception)}" +
                       (exception is null ? "" : $"{Environment.NewLine}{exception}");

            lock (writeLock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
    }
}
