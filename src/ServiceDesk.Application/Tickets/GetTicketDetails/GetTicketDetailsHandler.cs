using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetTicketDetails;

public sealed partial class GetTicketDetailsHandler(
    ICurrentUser currentUser,
    ITicketQueries ticketQueries,
    ILogger<GetTicketDetailsHandler> logger)
{
    public async Task<Result<TicketDetailsDto>> HandleAsync(GetTicketDetailsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2.
        var authentication = currentUser.RequireAuthenticated();
        if (authentication.IsFailure)
        {
            return authentication.Error;
        }

        // BR-02: the Admin sees every ticket, a Reporter only their own.
        string? reporterFilter;
        if (currentUser.IsInRole(Roles.Admin))
        {
            reporterFilter = null;
        }
        else if (currentUser.IsInRole(Roles.Reporter))
        {
            reporterFilter = authentication.Value;
        }
        else
        {
            return AuthErrors.Forbidden;
        }

        // Level 3: the filter is part of the query, so a foreign ticket is never loaded.
        var details = await ticketQueries.GetTicketDetailsAsync(query.TicketId, reporterFilter, ct);
        if (details is null)
        {
            if (reporterFilter is not null)
            {
                LogReporterTicketNotFound(logger, reporterFilter, query.TicketId); // BR-02
            }

            // BR-02: a foreign ticket gets the same answer as a missing one.
            return TicketErrors.NotFound;
        }

        return details;
    }

    // BR-02: a foreign and a missing ticket cannot be told apart here; only the Ids are logged, no ticket content.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reporter {UserId} requested ticket {TicketId}, which is not among their tickets")]
    private static partial void LogReporterTicketNotFound(ILogger logger, string userId, Guid ticketId);
}
