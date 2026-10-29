namespace ServiceDesk.Application.Tickets.GetMyTickets;

// PageNumber is 1-based; the list always belongs to the signed-in Reporter (ICurrentUser).
public sealed record GetMyTicketsQuery(int PageNumber = 1)
{
    public const int PageSize = 20;
}
