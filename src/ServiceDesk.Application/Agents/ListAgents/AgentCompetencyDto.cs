namespace ServiceDesk.Application.Agents.ListAgents;

// IsActive is false for a competency whose category was deactivated since; the agent keeps it (BR-13).
public sealed record AgentCompetencyDto(Guid CategoryId, string Code, string Name, bool IsActive);
