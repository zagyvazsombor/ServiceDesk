using Microsoft.Extensions.Logging;

namespace ServiceDesk.Web.Tests.UseCases;

// Keeps the log entries in memory, so a test can check the level and the exception of what was logged.
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
}
