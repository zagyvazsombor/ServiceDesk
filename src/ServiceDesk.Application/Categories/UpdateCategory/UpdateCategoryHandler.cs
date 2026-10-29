using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Categories.UpdateCategory;

public sealed partial class UpdateCategoryHandler(
    ICurrentUser currentUser,
    IValidator<UpdateCategoryCommand> validator,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<UpdateCategoryHandler> logger)
{
    public async Task<Result> HandleAsync(UpdateCategoryCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin manages the master data.
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

        var category = await categoryRepository.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        // An inactive category can be edited too; it stays inactive.
        var rename = category.Rename(command.Name);
        if (rename.IsFailure)
        {
            return rename;
        }

        var description = category.UpdateDescription(command.Description ?? string.Empty);
        if (description.IsFailure)
        {
            return description;
        }

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogCategoryUpdated(logger, category.Id, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Category {CategoryId} updated by {ActorUserId}")]
    private static partial void LogCategoryUpdated(ILogger logger, Guid categoryId, string actorUserId);
}
