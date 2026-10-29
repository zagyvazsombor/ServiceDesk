using Microsoft.Extensions.Logging;

namespace ServiceDesk.Application.Tests;

// Properties: the named values of the message template (e.g. "TicketId") and "{OriginalFormat}".
internal sealed record LogEntry(LogLevel Level, Exception? Exception, IReadOnlyDictionary<string, object?> Properties);
