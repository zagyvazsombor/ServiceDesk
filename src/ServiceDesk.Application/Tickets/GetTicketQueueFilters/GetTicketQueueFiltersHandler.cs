using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Tickets.GetTicketQueueFilters;

public sealed class GetTicketQueueFiltersHandler(ICurrentUser currentUser, ITicketQueries ticketQueries)
{
    public async Task<Result<TicketQueueFiltersDto>> HandleAsync(GetTicketQueueFiltersQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2: the filters serve the queue, which belongs to the Admin role.
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        return await ticketQueries.GetTicketQueueFiltersAsync(ct);
    }
}
