using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.AssignTicketManually;

public sealed partial class AssignTicketManuallyHandler(
    ICurrentUser currentUser,
    IValidator<AssignTicketManuallyCommand> validator,
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<AssignTicketManuallyHandler> logger)
{
    public async Task<Result> HandleAsync(AssignTicketManuallyCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin assigns by hand (AGENTS.md §10, state machine).
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

        var agent = await agentRepository.GetByIdAsync(command.AgentId, ct);
        if (agent is null)
        {
            return AgentErrors.NotFound;
        }

        // BR-03 and BR-04 are checked by the domain. A New ticket becomes Assigned; a reassignment keeps the status.
        var assignment = ticket.AssignTo(agent, authorization.Value, timeProvider.GetUtcNow());
        if (assignment.IsFailure)
        {
            return assignment;
        }

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogAssigned(logger, ticket.Id, agent.Id, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} assigned to agent {AssigneeAgentId} by {ActorUserId}")]
    private static partial void LogAssigned(ILogger logger, Guid ticketId, Guid assigneeAgentId, string actorUserId);
}
