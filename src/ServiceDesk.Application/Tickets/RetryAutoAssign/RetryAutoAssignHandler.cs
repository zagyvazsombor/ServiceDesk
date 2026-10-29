using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.RetryAutoAssign;

public sealed partial class RetryAutoAssignHandler(
    ICurrentUser currentUser,
    ITicketRepository ticketRepository,
    TicketAssignmentService assignmentService,
    IUnitOfWork unitOfWork,
    ILogger<RetryAutoAssignHandler> logger)
{
    // On success the value is the Id of the new assignee; null when still nobody is eligible (BR-06), which is not an
    // error: the ticket stays New, without an assignee.
    public async Task<Result<Guid?>> HandleAsync(RetryAutoAssignCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin reruns the automatic assignment (BR-06).
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

        // BR-06: only a ticket left without an assignee; a New ticket never has one. A closed ticket is refused too (BR-03).
        if (!TicketWorkflow.CanRetryAutoAssign(ticket.Status))
        {
            return TicketErrors.AutoAssignmentNotAllowed;
        }

        var assignment = await assignmentService.AutoAssignAsync(ticket, ct); // BR-04, BR-05
        if (assignment.IsFailure || assignment.Value is null)
        {
            return assignment; // On null nothing changed, so there is nothing to save.
        }

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save.Error;
        }

        LogAutoAssignmentRetried(logger, ticket.Id, assignment.Value.Value, authorization.Value);
        return assignment;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} assigned to agent {AssigneeAgentId} by the automatic assignment rerun by {ActorUserId}")]
    private static partial void LogAutoAssignmentRetried(ILogger logger, Guid ticketId, Guid assigneeAgentId, string actorUserId);
}
