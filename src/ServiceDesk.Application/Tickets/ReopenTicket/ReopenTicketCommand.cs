namespace ServiceDesk.Application.Tickets.ReopenTicket;

// Resolved → InProgress (AGENTS.md §10, state machine).
public sealed record ReopenTicketCommand(Guid TicketId);
