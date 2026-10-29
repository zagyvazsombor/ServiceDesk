namespace ServiceDesk.Application.Agents.ListAgents;

// Id: the value of the agent commands. Load: the priority weight sum of the open (Assigned, InProgress) tickets of the
// agent (BR-05); OpenTicketCount: the number of those tickets. Competencies are in name order.
public sealed record AgentListItemDto(
    Guid Id,
    string DisplayName,
    bool IsActive,
    IReadOnlyList<AgentCompetencyDto> Competencies,
    int Load,
    int OpenTicketCount);
