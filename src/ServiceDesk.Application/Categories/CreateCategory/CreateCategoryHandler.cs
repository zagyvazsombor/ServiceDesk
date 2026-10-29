using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Categories.CreateCategory;

public sealed partial class CreateCategoryHandler(
    ICurrentUser currentUser,
    IValidator<CreateCategoryCommand> validator,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreateCategoryHandler> logger)
{
    // On success the value is the Id of the new category.
    public async Task<Result<Guid>> HandleAsync(CreateCategoryCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin manages the master data.
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        // The validator also refuses a code that already exists.
        var validation = await validator.ValidateToResultAsync(command, ct);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var creation = Category.Create(command.Code, command.Name, command.Description ?? string.Empty);
        if (creation.IsFailure)
        {
            return creation.Error;
        }

        var category = creation.Value;
        categoryRepository.Add(category);

        // A duplicate saved concurrently is refused by the unique index: CategoryErrors.CodeAlreadyExists.
        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save.Error;
        }

        LogCategoryCreated(logger, category.Id, category.Code, authorization.Value);
        return category.Id;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Category {CategoryId} ({CategoryCode}) created by {ActorUserId}")]
    private static partial void LogCategoryCreated(ILogger logger, Guid categoryId, string categoryCode, string actorUserId);
}
