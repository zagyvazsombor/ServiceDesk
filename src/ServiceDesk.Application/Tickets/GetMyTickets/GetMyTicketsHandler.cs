using FluentValidation;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Tickets.GetMyTickets;

public sealed class GetMyTicketsHandler(
    ICurrentUser currentUser,
    IValidator<GetMyTicketsQuery> validator,
    ITicketQueries ticketQueries)
{
    public async Task<Result<PagedResult<MyTicketDto>>> HandleAsync(GetMyTicketsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2: the own ticket list belongs to the Reporter role.
        var authorization = currentUser.RequireRole(Roles.Reporter);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var validation = await validator.ValidateToResultAsync(query, ct);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        // BR-02, level 3: the reporter filter is part of the query.
        return await ticketQueries.GetMyTicketsAsync(authorization.Value, query.PageNumber, GetMyTicketsQuery.PageSize, ct);
    }
}
