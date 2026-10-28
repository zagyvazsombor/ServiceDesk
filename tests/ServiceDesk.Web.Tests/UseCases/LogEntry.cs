using Microsoft.Extensions.Logging;

namespace ServiceDesk.Web.Tests.UseCases;

// Properties: the named values of the message template, e.g. "Handler" or "UserId".
internal sealed record LogEntry(LogLevel Level, Exception? Exception, IReadOnlyDictionary<string, object?> Properties);
