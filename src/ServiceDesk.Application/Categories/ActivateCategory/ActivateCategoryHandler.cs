using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Categories.ActivateCategory;

public sealed partial class ActivateCategoryHandler(
    ICurrentUser currentUser,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<ActivateCategoryHandler> logger)
{
    public async Task<Result> HandleAsync(ActivateCategoryCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin manages the master data.
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var category = await categoryRepository.GetByIdAsync(command.CategoryId, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        if (category.IsActive)
        {
            return Result.Success(); // Already active: nothing changes, so nothing is saved.
        }

        // BR-13: from now on the category can be chosen for a new ticket and added as a competency again.
        category.Activate();

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogCategoryActivated(logger, category.Id, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Category {CategoryId} activated by {ActorUserId}")]
    private static partial void LogCategoryActivated(ILogger logger, Guid categoryId, string actorUserId);
}
