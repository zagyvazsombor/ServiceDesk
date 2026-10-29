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

        var categoryIds = command.CategoryIds.ToHashSet();
        var categories = await categoryRepository.GetByIdsAsync(categoryIds, ct);
        if (categories.Count != categoryIds.Count)
        {
            return CategoryErrors.NotFound;
        }

        // BR-13: a new competency needs an active category; one the agent already has stays.
        var update = agent.UpdateCompetencies(categories);
        if (update.IsFailure)
        {
            return update;
        }

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogCompetenciesSet(logger, agent.Id, categoryIds.Count, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Competencies of agent {AgentId} set to {CompetencyCount} categories by {ActorUserId}")]
    private static partial void LogCompetenciesSet(ILogger logger, Guid agentId, int competencyCount, string actorUserId);
}
