using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Agents.ActivateAgent;

public sealed partial class ActivateAgentHandler(
    ICurrentUser currentUser,
    IAgentRepository agentRepository,
    IUnitOfWork unitOfWork,
    ILogger<ActivateAgentHandler> logger)
{
    public async Task<Result> HandleAsync(ActivateAgentCommand command, CancellationToken ct)
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

        if (agent.IsActive)
        {
            return Result.Success(); // Already active: nothing changes, so nothing is saved.
        }

        // BR-04: from now on the agent can get tickets of its competencies again.
        agent.Activate();

        var save = await unitOfWork.SaveChangesAsync(ct);
        if (save.IsFailure)
        {
            return save;
        }

        LogAgentActivated(logger, agent.Id, authorization.Value);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Agent {AgentId} activated by {ActorUserId}")]
    private static partial void LogAgentActivated(ILogger logger, Guid agentId, string actorUserId);
}
