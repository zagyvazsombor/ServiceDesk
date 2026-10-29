using FluentValidation;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Tickets.GetTicketQueue;

public sealed class GetTicketQueueHandler(
    ICurrentUser currentUser,
    IValidator<GetTicketQueueQuery> validator,
    ITicketQueries ticketQueries)
{
    public async Task<Result<PagedResult<TicketQueueItemDto>>> HandleAsync(GetTicketQueueQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2: the queue of every ticket belongs to the Admin role (BR-02).
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var validation = await validator.ValidateToResultAsync(query, ct);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        return await ticketQueries.GetTicketQueueAsync(query, GetTicketQueueQuery.PageSize, ct);
    }
}
