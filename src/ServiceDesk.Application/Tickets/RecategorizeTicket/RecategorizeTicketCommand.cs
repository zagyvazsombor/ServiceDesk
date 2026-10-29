namespace ServiceDesk.Application.Tickets.RecategorizeTicket;

// BR-07: the assignee is not part of the command; keeping or changing it follows from the new category.
public sealed record RecategorizeTicketCommand(Guid TicketId, Guid CategoryId);
