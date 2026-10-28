using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Errors;

public static class GeneralErrors
{
    // AGENTS.md §7: optimistic concurrency conflict on save.
    public static readonly Error ConcurrencyConflict = new(
        "General.ConcurrencyConflict",
        "A jegyet időközben módosították, frissítsd az oldalt.",
        ErrorType.Conflict);
}
