using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets;

// AGENTS.md §5: the shared assignment logic, not a handler. The calling handler authorizes and saves; this service
// only changes the tracked ticket and agent.
public sealed class TicketAssignmentService(
    IAgentRepository agentRepository,
    IAssignmentStrategy assignmentStrategy,
    TimeProvider timeProvider)
{
    public async Task<Result> AutoAssignAsync(Ticket ticket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var candidates = await agentRepository.GetEligibleCandidatesAsync(ticket.CategoryId, ct); // BR-04
        var agentId = assignmentStrategy.SelectAgent(candidates); // BR-05
        if (agentId is null)
        {
            return Result.Success(); // BR-06: the ticket stays New, without an assignee.
        }

        // Agents are never deleted, so a candidate that cannot be loaded is a programming error.
        var agent = await agentRepository.GetByIdAsync(agentId.Value, ct)
            ?? throw new InvalidOperationException($"The selected agent {agentId} does not exist.");

        // null actor = system. AssignTo checks BR-03 and BR-04 again and records the assignment on the agent (BR-05).
        return ticket.AssignTo(agent, actorUserId: null, timeProvider.GetUtcNow());
    }
}
