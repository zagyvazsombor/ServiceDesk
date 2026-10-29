namespace ServiceDesk.Application.Tickets.RetryAutoAssign;

public sealed record RetryAutoAssignCommand(Guid TicketId);
