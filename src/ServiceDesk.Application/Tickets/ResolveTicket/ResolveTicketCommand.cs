namespace ServiceDesk.Application.Tickets.ResolveTicket;

// InProgress → Resolved (AGENTS.md §10, state machine).
public sealed record ResolveTicketCommand(Guid TicketId);
