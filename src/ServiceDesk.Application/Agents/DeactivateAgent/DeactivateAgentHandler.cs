using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Agents.DeactivateAgent;

// BR-12: the tickets of the agent are not loaded at all, so the deactivation cannot change them.
public sealed partial class DeactivateAgentHandler(
    ICurrentUser currentUser,
    IAgentRepository agentRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeactivateAgentHandler> logger)
{
    public async Task<Result> HandleAsync(DeactivateAgentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AGENTS.md §8, level 2: only an Admin manages the master data.
        var authorization = currentUser.RequireRole(Roles.Admin);
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        var agent = await agentRepository.GetByIdAsync(command.AgentId, ct);
        if (agent is null)
        {
            return AgentErrors.NotFound;
        }

        if (!agent.IsActive)
        {
            return Result.Success(); // Already inactive: nothing changes, so nothing is saved.
        }

        // BR-12: the assigned tickets stay; BR-04: from now on the agent gets no new ticket.
        agent.Deactivate();

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogAgentDeactivated(logger, agent.Id, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent {AgentId} deactivated by {ActorUserId}")]
    private static partial void LogAgentDeactivated(ILogger logger, Guid agentId, string actorUserId);
}
