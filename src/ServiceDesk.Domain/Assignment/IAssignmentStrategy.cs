namespace ServiceDesk.Domain.Assignment;

public interface IAssignmentStrategy
{
    // Picks one agent from candidates that the caller has already filtered for eligibility (BR-04);
    // Ticket.AssignTo checks BR-04 again. Returns null when there is no candidate (BR-06).
    Guid? SelectAgent(IReadOnlyList<AgentCandidate> candidates);
}
