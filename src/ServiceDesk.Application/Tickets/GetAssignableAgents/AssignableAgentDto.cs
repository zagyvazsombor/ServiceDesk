namespace ServiceDesk.Application.Tickets.GetAssignableAgents;

// AgentId: the value of AssignTicketManuallyCommand.AgentId. Load: the priority weight sum of the open (Assigned,
// InProgress) tickets of the agent (BR-05); OpenTicketCount: the number of those tickets.
public sealed record AssignableAgentDto(
    Guid AgentId,
    string DisplayName,
    int Load,
    int OpenTicketCount,
    bool IsCurrentAssignee);
