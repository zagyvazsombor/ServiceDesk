using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetTicketQueue;

// PageNumber is 1-based. Every filter is optional (null or false = no filter); AssigneeAgentId and UnassignedOnly
// cannot be combined. The closed tickets are left out unless IncludeClosed is set, which a Closed status filter needs.
public sealed record GetTicketQueueQuery(
    int PageNumber = 1,
    TicketStatus? Status = null,
    Guid? CategoryId = null,
    Priority? Priority = null,
    Guid? AssigneeAgentId = null,
    bool UnassignedOnly = false,
    bool IncludeClosed = false)
{
    public const int PageSize = 20;
}
