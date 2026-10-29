namespace ServiceDesk.Application.Agents.SetAgentCompetencies;

// CategoryIds: the whole new competency set of the agent; an empty list removes every competency, except the current
// ones of inactive categories, which always stay (BR-13).
public sealed record SetAgentCompetenciesCommand(Guid AgentId, IReadOnlyList<Guid> CategoryIds);
