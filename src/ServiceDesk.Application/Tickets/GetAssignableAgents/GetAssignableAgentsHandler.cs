using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetAssignableAgents;

public sealed class GetAssignableAgentsHandler(ICurrentUser currentUser, ITicketQueries ticketQueries)
{
    public async Task<Result<AssignableAgentsDto>> HandleAsync(GetAssignableAgentsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2: the list serves the manual assignment, which belongs to the Admin role.
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var assignableAgents = await ticketQueries.GetAssignableAgentsAsync(query.TicketId, ct);
        if (assignableAgents is null)
        {
            return TicketErrors.NotFound;
        }

        // BR-03: a closed ticket cannot be assigned, so there is nobody to offer.
        if (assignableAgents.TicketStatus == TicketStatus.Closed)
        {
            return TicketErrors.ClosedCannotBeAssigned;
        }

        return assignableAgents;
    }
}
