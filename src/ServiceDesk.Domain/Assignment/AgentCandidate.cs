using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Domain.Assignment;

// An agent already eligible for the ticket (BR-04), with the priorities of its open (Assigned, InProgress) tickets.
public sealed record AgentCandidate(
    Guid AgentId,
    DateTimeOffset? LastAssignedAt,
    IReadOnlyList<Priority> OpenTicketPriorities)
{
    public IReadOnlyList<Priority> OpenTicketPriorities { get; } =
        OpenTicketPriorities ?? throw new ArgumentNullException(nameof(OpenTicketPriorities));

    // BR-05: the load is the sum of the priority weights of the open tickets.
    public int Load => OpenTicketPriorities.Sum(PriorityMatrix.GetLoadWeight);
}
