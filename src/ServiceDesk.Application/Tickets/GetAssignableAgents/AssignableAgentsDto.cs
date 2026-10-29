using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetAssignableAgents;

// Agents: the active agents with a competency for the category of the ticket (BR-04), least loaded first, then by
// name. Empty when nobody is eligible (BR-06).
public sealed record AssignableAgentsDto(TicketStatus TicketStatus, IReadOnlyList<AssignableAgentDto> Agents);
