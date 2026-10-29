namespace ServiceDesk.Application.Tickets.StartWork;

// Assigned → InProgress (AGENTS.md §10, state machine).
public sealed record StartWorkCommand(Guid TicketId);
