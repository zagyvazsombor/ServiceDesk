using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetTicketDetails;

// ActorName: the agent name of an Admin, otherwise the e-mail address; null = system.
// OldValue / NewValue: agent and category names for the assignment and recategorization entries, otherwise the stored
// value (e.g. the status name).
public sealed record TicketHistoryEntryDto(
    int Sequence,
    DateTimeOffset At,
    TicketHistoryAction Action,
    string? ActorName,
    string? OldValue,
    string? NewValue);
