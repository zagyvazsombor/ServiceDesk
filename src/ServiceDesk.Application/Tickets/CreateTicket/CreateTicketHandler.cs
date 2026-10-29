using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.CreateTicket;

public sealed partial class CreateTicketHandler(
    ICurrentUser currentUser,
    IValidator<CreateTicketCommand> validator,
    ICategoryRepository categoryRepository,
    ICategorySuggestionRepository suggestionRepository,
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

        // BR-09: checked before the ticket is created, so a refused suggestion leaves nothing to save.
        CategorySuggestion? suggestion = null;
        if (command.SuggestionId is { } suggestionId)
        {
            suggestion = await suggestionRepository.GetByIdAsync(suggestionId, ct);
            if (suggestion is null || suggestion.RequestedByUserId != authorization.Value)
            {
                LogSuggestionNotFound(logger, authorization.Value, suggestionId);

                // A suggestion of another user gets the same answer as a missing one.
                return CategorySuggestionErrors.NotFound;
            }

            if (suggestion.Decision != CategorySuggestionDecision.Pending)
            {
                return CategorySuggestionErrors.AlreadyDecided;
            }
        }

        // BR-01: the reporter is the signed-in user. BR-13: Ticket.Create refuses an inactive category.
        // Impact and Urgency are not null here, the validator requires them.
        var now = timeProvider.GetUtcNow();
        var creation = Ticket.Create(
            authorization.Value,
            command.Title,
            command.Description,
            command.Impact!.Value,
            command.Urgency!.Value,
            category,
            now);
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

        // BR-09: the human decision follows from the category the Reporter chose.
        if (suggestion is not null)
        {
            var decision = ticket.CategoryId == suggestion.SuggestedCategoryId
                ? CategorySuggestionDecision.Accepted
                : CategorySuggestionDecision.Overridden;
            var decide = suggestion.Decide(ticket.Id, decision, now);
            if (decide.IsFailure)
            {
                return decide.Error;
            }
        }

        // The ticket, its history, the assignment of the agent and the decision are saved together.
        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save.Error;
        }

        LogTicketCreated(logger, ticket.Id, ticket.ReporterId, assignment.Value);
        if (suggestion is not null)
        {
            LogSuggestionDecided(logger, suggestion.Id, ticket.Id, suggestion.Decision);
        }

        return ticket.Id;
    }

    // AGENTS.md §8, §11: a business event, without the title and the description.
    [LoggerMessage(Level = LogLevel.Information, Message = "Ticket {TicketId} created by {ReporterId}, assigned to agent {AssigneeAgentId}")]
    private static partial void LogTicketCreated(ILogger logger, Guid ticketId, string reporterId, Guid? assigneeAgentId);

    // BR-09: a business event, without the reasoning.
    [LoggerMessage(Level = LogLevel.Information, Message = "Category suggestion {SuggestionId} decided for ticket {TicketId}: {Decision}")]
    private static partial void LogSuggestionDecided(ILogger logger, Guid suggestionId, Guid ticketId, CategorySuggestionDecision decision);

    // AGENTS.md §11: a missing and a foreign suggestion cannot be told apart here; a denied access is a Warning.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reporter {UserId} referred to category suggestion {SuggestionId}, which is not among their suggestions")]
    private static partial void LogSuggestionNotFound(ILogger logger, string userId, Guid suggestionId);
}
