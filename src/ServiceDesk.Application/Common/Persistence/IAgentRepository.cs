using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Assignment;

namespace ServiceDesk.Application.Common.Persistence;

public interface IAgentRepository
{
    // Loads the whole aggregate, including its competencies; null if the agent does not exist.
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct);

    // BR-04: only active agents with a competency for the category, each with the priorities of its open
    // (Assigned, InProgress) tickets for the load calculation (BR-05). Empty if nobody is eligible (BR-06).
    Task<IReadOnlyList<AgentCandidate>> GetEligibleCandidatesAsync(Guid categoryId, CancellationToken ct);
}
