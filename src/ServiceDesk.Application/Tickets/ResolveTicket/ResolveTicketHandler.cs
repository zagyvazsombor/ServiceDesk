using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.ResolveTicket;

public sealed partial class ResolveTicketHandler(
    ICurrentUser currentUser,
    ITicketRepository ticketRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ResolveTicketHandler> logger)
{
    public async Task<Result> HandleAsync(ResolveTicketCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin resolves (AGENTS.md §10, state machine).
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
        var transition = ticket.Resolve(authorization.Value, timeProvider.GetUtcNow());
        if (transition.IsFailure)
        {
            return transition;
        }

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogResolved(logger, ticket.Id, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} resolved by {ActorUserId}")]
    private static partial void LogResolved(ILogger logger, Guid ticketId, string actorUserId);
}
