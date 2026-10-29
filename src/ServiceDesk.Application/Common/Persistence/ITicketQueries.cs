using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Application.Tickets.GetTicketDetails;

namespace ServiceDesk.Application.Common.Persistence;

// Read side of the tickets: projections straight into the DTOs, without tracking (AGENTS.md §7).
public interface ITicketQueries
{
    // BR-02: only the tickets of the reporter, newest first.
    Task<PagedResult<MyTicketDto>> GetMyTicketsAsync(string reporterId, int pageNumber, int pageSize, CancellationToken ct);

    // BR-13: only the active categories can be chosen for a new ticket.
    Task<IReadOnlyList<ActiveCategoryDto>> GetActiveCategoriesAsync(CancellationToken ct);

    // BR-02: with a reporterId the ticket is found only if it belongs to that reporter (AGENTS.md §8, level 3);
    // null means any ticket (Admin). null if the ticket is not found.
    Task<TicketDetailsDto?> GetTicketDetailsAsync(Guid ticketId, string? reporterId, CancellationToken ct);
}
