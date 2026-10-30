using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ServiceDesk.Web.Tests.UseCases;

namespace ServiceDesk.Web.Tests.Http;

// Keeps the log entries of the whole test server in memory with their category, so an HTTP-level test can check what
// the pipeline logged.
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(string Category, LogEntry Entry)> _entries = new();

    public IReadOnlyList<LogEntry> EntriesOf(string category) =>
        [.. _entries.Where(item => item.Category == category).Select(item => item.Entry)];

    public ILogger CreateLogger(string categoryName) => new CategoryLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class CategoryLogger(string category, ConcurrentQueue<(string Category, LogEntry Entry)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(value => value.Key, value => value.Value)
                : new Dictionary<string, object?>();
            entries.Enqueue((category, new LogEntry(logLevel, exception, properties)));
        }
    }
}
