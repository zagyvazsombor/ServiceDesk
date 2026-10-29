using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Tickets.GetActiveCategories;

public sealed class GetActiveCategoriesHandler(ICurrentUser currentUser, ITicketQueries ticketQueries)
{
    public async Task<Result<IReadOnlyList<ActiveCategoryDto>>> HandleAsync(GetActiveCategoriesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2: any signed-in user; the categories are not personal data.
        var authorization = currentUser.RequireAuthenticated();
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var categories = await ticketQueries.GetActiveCategoriesAsync(ct);
        return Result.Success(categories);
    }
}
