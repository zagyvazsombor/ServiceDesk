using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Agents.ListAgents;

public sealed class ListAgentsHandler(ICurrentUser currentUser, ITicketQueries ticketQueries)
{
    public async Task<Result<IReadOnlyList<AgentListItemDto>>> HandleAsync(ListAgentsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2: only an Admin manages the master data.
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var agents = await ticketQueries.GetAgentsAsync(ct);
        return Result.Success(agents);
    }
}
