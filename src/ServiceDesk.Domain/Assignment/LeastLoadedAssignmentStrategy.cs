namespace ServiceDesk.Domain.Assignment;

public sealed class LeastLoadedAssignmentStrategy : IAssignmentStrategy
{
    public Guid? SelectAgent(IReadOnlyList<AgentCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            return null; // BR-06
        }

        // BR-05: least load, then oldest LastAssignedAt (null first), then AgentId; the order is independent of the input.
        return candidates
            .OrderBy(candidate => candidate.Load)
            .ThenBy(candidate => candidate.LastAssignedAt.HasValue)
            .ThenBy(candidate => candidate.LastAssignedAt)
            .ThenBy(candidate => candidate.AgentId)
            .First()
            .AgentId;
    }
}
