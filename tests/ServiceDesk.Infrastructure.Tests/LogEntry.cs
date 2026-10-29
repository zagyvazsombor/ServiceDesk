using Microsoft.Extensions.Logging;

namespace ServiceDesk.Infrastructure.Tests;

// Message: the formatted text. Properties: the named values of the message template and "{OriginalFormat}".
internal sealed record LogEntry(
    LogLevel Level,
    Exception? Exception,
    string Message,
    IReadOnlyDictionary<string, object?> Properties);
