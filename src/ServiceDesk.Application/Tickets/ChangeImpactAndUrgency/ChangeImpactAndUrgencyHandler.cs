using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.ChangeImpactAndUrgency;

public sealed partial class ChangeImpactAndUrgencyHandler(
    ICurrentUser currentUser,
    IValidator<ChangeImpactAndUrgencyCommand> validator,
    ITicketRepository ticketRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<ChangeImpactAndUrgencyHandler> logger)
{
    public async Task<Result> HandleAsync(ChangeImpactAndUrgencyCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: triage belongs to the Admin role (BR-11).
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var validation = await validator.ValidateToResultAsync(command, ct);
        if (validation.IsFailure)
        {
            return validation;
        }

        var ticket = await ticketRepository.GetByIdAsync(command.TicketId, ct);
        if (ticket is null)
        {
            return TicketErrors.NotFound;
        }

        // BR-03: a closed ticket is refused. BR-11: the priority is recalculated from the matrix.
        // Impact and Urgency are not null here, the validator requires them.
        var change = ticket.ChangeImpactAndUrgency(
            command.Impact!.Value,
            command.Urgency!.Value,
            authorization.Value,
            timeProvider.GetUtcNow());
        if (change.IsFailure)
        {
            return change;
        }

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogTriaged(logger, ticket.Id, ticket.Priority, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} triaged to priority {Priority} by {ActorUserId}")]
    private static partial void LogTriaged(ILogger logger, Guid ticketId, Priority priority, string actorUserId);
}
