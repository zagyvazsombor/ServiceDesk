using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetTicketQueue;

// PageNumber is 1-based. Every filter is optional (null or false = no filter); AssigneeAgentId and UnassignedOnly
// cannot be combined.
public sealed record GetTicketQueueQuery(
    int PageNumber = 1,
    TicketStatus? Status = null,
    Guid? CategoryId = null,
    Priority? Priority = null,
    Guid? AssigneeAgentId = null,
    bool UnassignedOnly = false)
{
    public const int PageSize = 20;
}
