namespace ServiceDesk.Application.Agents.SetAgentCompetencies;

// CategoryIds: the whole new competency set of the agent; an empty list removes every competency.
public sealed record SetAgentCompetenciesCommand(Guid AgentId, IReadOnlyList<Guid> CategoryIds);
