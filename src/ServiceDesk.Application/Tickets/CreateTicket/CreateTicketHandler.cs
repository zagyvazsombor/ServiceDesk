using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.CreateTicket;

public sealed partial class CreateTicketHandler(
    ICurrentUser currentUser,
    IValidator<CreateTicketCommand> validator,
    ICategoryRepository categoryRepository,
    ITicketRepository ticketRepository,
    TicketAssignmentService assignmentService,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CreateTicketHandler> logger)
{
    // On success the value is the Id of the new ticket.
    public async Task<Result<Guid>> HandleAsync(CreateTicketCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only a Reporter creates tickets (BR-01).
        var authorization = currentUser.RequireRole(Roles.Reporter);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var validation = await validator.ValidateToResultAsync(command, ct);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var category = await categoryRepository.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        // BR-01: the reporter is the signed-in user. BR-13: Ticket.Create refuses an inactive category.
        // Impact and Urgency are not null here, the validator requires them.
        var creation = Ticket.Create(
            authorization.Value,
            command.Title,
            command.Description,
            command.Impact!.Value,
            command.Urgency!.Value,
            category,
            timeProvider.GetUtcNow());
        if (creation.IsFailure)
        {
            return creation.Error;
        }

        var ticket = creation.Value;
        ticketRepository.Add(ticket);

        var assignment = await assignmentService.AutoAssignAsync(ticket, ct); // BR-05, BR-06
        if (assignment.IsFailure)
        {
            return assignment.Error;
        }

        // The ticket, its history and the assignment of the agent are saved together.
        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save.Error;
        }

        LogTicketCreated(logger, ticket.Id, ticket.ReporterId, assignment.Value);
        return ticket.Id;
    }

    // AGENTS.md §8, §11: a business event, without the title and the description.
    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} created by {ReporterId}, assigned to agent {AssigneeAgentId}")]
    private static partial void LogTicketCreated(ILogger logger, Guid ticketId, string reporterId, Guid? assigneeAgentId);
}
