using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Ai;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Tickets.SuggestCategory;

public sealed partial class SuggestCategoryHandler(
    ICurrentUser currentUser,
    IValidator<SuggestCategoryCommand> validator,
    ISuggestionRateLimiter rateLimiter,
    ICategoryRepository categoryRepository,
    ICategorySuggester categorySuggester,
    ICategorySuggestionRepository suggestionRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<SuggestCategoryHandler> logger)
{
    public async Task<Result<CategorySuggestionDto>> HandleAsync(SuggestCategoryCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: the suggestion belongs to the create form, so only a Reporter asks for it (BR-01, BR-09).
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

        // AGENTS.md §8: 5 requests per minute and user; after the validation, so an invalid request costs nothing.
        var userId = authorization.Value;
        if (!rateLimiter.TryAcquire(userId))
        {
            LogRateLimited(logger, userId);
            return AiErrors.RateLimited;
        }

        // BR-08, BR-13: the model chooses only from the active categories; without one there is nothing to ask.
        var categories = await categoryRepository.GetActiveAsync(ct);
        if (categories.Count == 0)
        {
            return AiErrors.NoSuggestion;
        }

        // AGENTS.md §9: the title, the description and the categories only; no user Id or e-mail address.
        var input = new CategorySuggestionInput(
            command.Title.Trim(),
            command.Description.Trim(),
            categories
                .Select(category => new SuggestionCategory(category.Code, category.Name, category.Description))
                .ToList());
        var suggestion = await categorySuggester.SuggestAsync(input, ct);
        if (suggestion.IsFailure)
        {
            return suggestion.Error;
        }

        // BR-08: the adapter returns one of the input codes; looked up here, as the entity needs the category itself.
        var output = suggestion.Value;
        var suggestedCategory = categories.FirstOrDefault(category => category.Code == output.CategoryCode);
        if (suggestedCategory is null)
        {
            return AiErrors.NoSuggestion;
        }

        // BR-09: saved as Pending; the create form records the human decision about it.
        var creation = CategorySuggestion.Create(
            userId,
            suggestedCategory,
            output.Confidence,
            output.Reasoning,
            output.Model,
            output.PromptVersion,
            timeProvider.GetUtcNow());
        if (creation.IsFailure)
        {
            return creation.Error;
        }

        var categorySuggestion = creation.Value;
        suggestionRepository.Add(categorySuggestion);

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save.Error;
        }

        LogSuggestionCreated(logger, categorySuggestion.Id, userId, suggestedCategory.Code, categorySuggestion.Confidence);
        return new CategorySuggestionDto(
            categorySuggestion.Id,
            suggestedCategory.Id,
            suggestedCategory.Name,
            categorySuggestion.Confidence,
            categorySuggestion.Reasoning);
    }

    // AGENTS.md §11: a denied request is a Warning.
    [LoggerMessage(Level = LogLevel.Warning, Message = "Category suggestion rate limit exceeded by user {UserId}")]
    private static partial void LogRateLimited(ILogger logger, string userId);

    // AGENTS.md §8, §11: a business event, without the title, the description and the reasoning.
    [LoggerMessage(Level = LogLevel.Information, Message = "Category suggestion {SuggestionId} for user {UserId}: {CategoryCode} with confidence {Confidence}")]
    private static partial void LogSuggestionCreated(ILogger logger, Guid suggestionId, string userId, string categoryCode, double confidence);
}
