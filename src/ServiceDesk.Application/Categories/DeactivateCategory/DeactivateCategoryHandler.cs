using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Categories.DeactivateCategory;

public sealed partial class DeactivateCategoryHandler(
    ICurrentUser currentUser,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeactivateCategoryHandler> logger)
{
    public async Task<Result> HandleAsync(DeactivateCategoryCommand command, CancellationToken ct)
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

        if (!category.IsActive)
        {
            return Result.Success(); // Already inactive: nothing changes, so nothing is saved.
        }

        // BR-13: there is no deletion. The existing tickets and the competencies of the agents keep the category.
        category.Deactivate();

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogCategoryDeactivated(logger, category.Id, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Category {CategoryId} deactivated by {ActorUserId}")]
    private static partial void LogCategoryDeactivated(ILogger logger, Guid categoryId, string actorUserId);
}
