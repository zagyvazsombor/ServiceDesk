using FluentValidation;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Agents.SetAgentCompetencies;

public sealed partial class SetAgentCompetenciesHandler(
    ICurrentUser currentUser,
    IValidator<SetAgentCompetenciesCommand> validator,
    IAgentRepository agentRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    ILogger<SetAgentCompetenciesHandler> logger)
{
    public async Task<Result> HandleAsync(SetAgentCompetenciesCommand command, CancellationToken ct)
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

        var agent = await agentRepository.GetByIdAsync(command.AgentId, ct);
        if (agent is null)
        {
            return AgentErrors.NotFound;
        }

        // The categories of the current competencies are loaded too (in the same query), so the domain knows which of
        // them are inactive.
        var categoryIds = command.CategoryIds.ToHashSet();
        var currentCategoryIds = agent.Competencies.Select(competency => competency.CategoryId).ToHashSet();
        var loaded = await categoryRepository.GetByIdsAsync([.. categoryIds.Union(currentCategoryIds)], ct);
        var categories = loaded.Where(category => categoryIds.Contains(category.Id)).ToList();
        if (categories.Count != categoryIds.Count)
        {
            return CategoryErrors.NotFound;
        }

        // BR-13: a new competency needs an active category; a current one of an inactive category stays, even if the
        // command leaves it out.
        var currentCategories = loaded.Where(category => currentCategoryIds.Contains(category.Id)).ToList();
        var update = agent.UpdateCompetencies(categories, currentCategories);
        if (update.IsFailure)
        {
            return update;
        }

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        // The resulting count, the kept competencies of inactive categories included.
        LogCompetenciesSet(logger, agent.Id, agent.Competencies.Count, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Competencies of agent {AgentId} set to {CompetencyCount} categories by {ActorUserId}")]
    private static partial void LogCompetenciesSet(ILogger logger, Guid agentId, int competencyCount, string actorUserId);
}
