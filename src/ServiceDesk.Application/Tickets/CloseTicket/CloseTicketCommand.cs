namespace ServiceDesk.Application.Tickets.CloseTicket;

// Resolved → Closed, which is terminal (BR-03).
public sealed record CloseTicketCommand(Guid TicketId);
