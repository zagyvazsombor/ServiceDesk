using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.RecategorizeTicket;

public sealed partial class RecategorizeTicketHandler(
    ICurrentUser currentUser,
    IValidator<RecategorizeTicketCommand> validator,
    ITicketRepository ticketRepository,
    ICategoryRepository categoryRepository,
    IAgentRepository agentRepository,
    TicketAssignmentService assignmentService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<RecategorizeTicketHandler> logger)
{
    public async Task<Result> HandleAsync(RecategorizeTicketCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin recategorizes (BR-07).
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

        var category = await categoryRepository.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        var now = timeProvider.GetUtcNow();
        var previousCategoryId = ticket.CategoryId;

        // BR-03, BR-07: only a New, Assigned or InProgress ticket. BR-13: only an active category.
        var recategorization = ticket.Recategorize(category, authorization.Value, now);
        if (recategorization.IsFailure)
        {
            return recategorization;
        }

        if (ticket.CategoryId == previousCategoryId)
        {
            return Result.Success(); // The same category: nothing changed, so the assignment stays as it is.
        }

        var assignment = await UpdateAssignmentAsync(ticket, now, ct);
        if (assignment.IsFailure)
        {
            return assignment;
        }

        // The category, the assignment and their history are saved together.
        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogRecategorized(logger, ticket.Id, ticket.CategoryId, authorization.Value, ticket.AssigneeAgentId);
        return Result.Success();
    }

    // BR-07: an eligible assignee keeps the ticket; otherwise the automatic assignment runs (BR-05), and when nobody is
    // eligible the assignee is removed. A New ticket has no assignee, so it simply gets the automatic assignment.
    private async Task<Result> UpdateAssignmentAsync(Ticket ticket, DateTimeOffset now, CancellationToken ct)
    {
        if (ticket.AssigneeAgentId is { } assigneeId)
        {
            // Agents are never deleted, so an assignee that cannot be loaded is a programming error.
            var assignee = await agentRepository.GetByIdAsync(assigneeId, ct)
                ?? throw new InvalidOperationException($"The assignee agent {assigneeId} does not exist.");
            if (assignee.IsEligibleFor(ticket.CategoryId))
            {
                return Result.Success(); // BR-07
            }
        }

        var assignment = await assignmentService.AutoAssignAsync(ticket, ct); // BR-05
        if (assignment.IsFailure)
        {
            return assignment;
        }

        // BR-07: no eligible agent, so the ticket returns to New; a New ticket stays as it is (BR-06).
        if (assignment.Value is null && ticket.AssigneeAgentId is not null)
        {
            return ticket.Unassign(now);
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} recategorized to {CategoryId} by {ActorUserId}, assigned to agent {AssigneeAgentId}")]
    private static partial void LogRecategorized(ILogger logger, Guid ticketId, Guid categoryId, string actorUserId, Guid? assigneeAgentId);
}
