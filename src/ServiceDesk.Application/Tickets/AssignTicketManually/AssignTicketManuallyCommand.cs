namespace ServiceDesk.Application.Tickets.AssignTicketManually;

// AgentId: the agent who gets the ticket (an Agent Id, not a user Id).
public sealed record AssignTicketManuallyCommand(Guid TicketId, Guid AgentId);
