using Microsoft.Extensions.Logging;

namespace ServiceDesk.Web.Tests.UseCases;

// Keeps the log entries in memory, so a test can check the level, the exception and the message template values.
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // A message template call passes its named values as key-value pairs.
        var properties = state is IEnumerable<KeyValuePair<string, object?>> values
            ? values.ToDictionary(value => value.Key, value => value.Value)
            : new Dictionary<string, object?>();
        Entries.Add(new LogEntry(logLevel, exception, properties));
    }
}
