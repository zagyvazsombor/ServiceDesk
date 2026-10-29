using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Categories.ListCategories;

public sealed class ListCategoriesHandler(ICurrentUser currentUser, ITicketQueries ticketQueries)
{
    public async Task<Result<IReadOnlyList<CategoryListItemDto>>> HandleAsync(ListCategoriesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2: only an Admin manages the master data.
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var categories = await ticketQueries.GetCategoriesAsync(ct);
        return Result.Success(categories);
    }
}
