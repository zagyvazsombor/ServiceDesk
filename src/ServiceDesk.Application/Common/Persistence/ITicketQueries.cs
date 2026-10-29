using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.GetAssignableAgents;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Application.Tickets.GetTicketQueue;
using ServiceDesk.Application.Tickets.GetTicketQueueFilters;

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

    // Admin queue: the tickets matching every filter of the query; highest priority first, then oldest first.
    Task<PagedResult<TicketQueueItemDto>> GetTicketQueueAsync(GetTicketQueueQuery query, int pageSize, CancellationToken ct);

    // The choices of the queue filters: every category and agent by name, the inactive ones too (BR-12, BR-13).
    Task<TicketQueueFiltersDto> GetTicketQueueFiltersAsync(CancellationToken ct);

    // BR-04: the active agents with a competency for the category of the ticket, with their load (BR-05);
    // null if the ticket is not found.
    Task<AssignableAgentsDto?> GetAssignableAgentsAsync(Guid ticketId, CancellationToken ct);
}
