using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.ReopenTicket;

public sealed partial class ReopenTicketHandler(
    ICurrentUser currentUser,
    ITicketRepository ticketRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ReopenTicketHandler> logger)
{
    public async Task<Result> HandleAsync(ReopenTicketCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin reopens (AGENTS.md §10, state machine).
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var ticket = await ticketRepository.GetByIdAsync(command.TicketId, ct);
        if (ticket is null)
        {
            return TicketErrors.NotFound;
        }

        // Any other status is Ticket.InvalidTransition; a closed ticket is terminal (BR-03).
        var transition = ticket.Reopen(authorization.Value, timeProvider.GetUtcNow());
        if (transition.IsFailure)
        {
            return transition;
        }

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogReopened(logger, ticket.Id, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} reopened by {ActorUserId}")]
    private static partial void LogReopened(ILogger logger, Guid ticketId, string actorUserId);
}
