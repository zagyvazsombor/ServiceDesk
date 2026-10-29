using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDesk.Application.Agents.ActivateAgent;
using ServiceDesk.Application.Agents.DeactivateAgent;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Agents;

// DeactivateAgent and ActivateAgent differ only in the direction of the change (BR-04, BR-12).
public sealed class AgentActivationHandlersTests
{
    private readonly IAgentRepository _agents = Substitute.For<IAgentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Category _hardware = TestData.CreateCategory();

    public AgentActivationHandlersTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    public enum Operation
    {
        Deactivate,
        Activate,
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(Operation.Deactivate)]
    [InlineData(Operation.Activate)]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(Operation operation)
    {
        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Anonymous(), Guid.CreateVersion7());

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Theory]
    [InlineData(Operation.Deactivate)]
    [InlineData(Operation.Activate)]
    [Trait("BR", "BR-12")]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(Operation operation)
    {
        // Arrange
        var agent = AgentCanBeLoaded(AgentBefore(operation));
        var isActive = agent.IsActive;

        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Reporter(), agent.Id);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        agent.IsActive.ShouldBe(isActive);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Theory]
    [InlineData(Operation.Deactivate)]
    [InlineData(Operation.Activate)]
    public async Task HandleAsync_UnknownAgent_ReturnsNotFound(Operation operation)
    {
        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Admin(), Guid.CreateVersion7());

        // Assert
        result.Error.ShouldBe(AgentErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Theory]
    [InlineData(Operation.Deactivate, false)]
    [InlineData(Operation.Activate, true)]
    [Trait("BR", "BR-04")]
    [Trait("BR", "BR-12")]
    public async Task HandleAsync_AgentInOtherState_ChangesStateKeepsCompetenciesSavesAndLogs(
        Operation operation,
        bool isActiveAfter)
    {
        // Arrange
        var agent = AgentCanBeLoaded(AgentBefore(operation));

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), agent.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        agent.IsActive.ShouldBe(isActiveAfter);
        agent.IsEligibleFor(_hardware.Id).ShouldBe(isActiveAfter);
        agent.Competencies.ShouldHaveSingleItem().CategoryId.ShouldBe(_hardware.Id);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        var entry = log.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        (entry.Properties["AgentId"], entry.Properties["ActorUserId"]).ShouldBe((agent.Id, TestUsers.UserId));
    }

    [Theory]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [Trait("BR", "BR-12")]
    public async Task HandleAsync_DeactivateAgentWithOpenTicket_LeavesTicketUnchanged(TicketStatus status)
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _hardware));
        var ticket = TestData.CreateTicketInStatus(status, _hardware, agent);
        var historyCount = ticket.History.Count;

        // Act
        var (result, _) = await RunAsync(Operation.Deactivate, TestUsers.Admin(), agent.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((status, agent.Id));
        ticket.History.Count.ShouldBe(historyCount);
    }

    [Theory]
    [InlineData(Operation.Deactivate, false)]
    [InlineData(Operation.Activate, true)]
    public async Task HandleAsync_AgentAlreadyInTargetState_ReturnsSuccessWithoutSaveOrLog(Operation operation, bool isActive)
    {
        // Arrange
        var agent = TestData.CreateAgent("Kiss Anna", _hardware);
        if (!isActive)
        {
            agent.Deactivate();
        }

        AgentCanBeLoaded(agent);

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), agent.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        agent.IsActive.ShouldBe(isActive);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
        log.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Operation.Deactivate)]
    [InlineData(Operation.Activate)]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog(Operation operation)
    {
        // Arrange
        var agent = AgentCanBeLoaded(AgentBefore(operation));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), agent.Id);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        log.ShouldBeEmpty();
    }

    // The deactivation starts from an active agent, the activation from an inactive one; both have HARDWARE.
    private Agent AgentBefore(Operation operation)
    {
        var agent = TestData.CreateAgent("Kiss Anna", _hardware);
        if (operation == Operation.Activate)
        {
            agent.Deactivate();
        }

        return agent;
    }

    private static async Task<(Result Result, IReadOnlyList<LogEntry> Log)> RunWithLoggerAsync<THandler>(
        Func<RecordingLogger<THandler>, Task<Result>> run)
    {
        var logger = new RecordingLogger<THandler>();
        var result = await run(logger);
        return (result, logger.Entries);
    }

    private Task<(Result Result, IReadOnlyList<LogEntry> Log)> RunAsync(
        Operation operation,
        ICurrentUser currentUser,
        Guid agentId) => operation switch
        {
            Operation.Deactivate => RunWithLoggerAsync<DeactivateAgentHandler>(logger =>
                new DeactivateAgentHandler(currentUser, _agents, _unitOfWork, logger)
                    .HandleAsync(new DeactivateAgentCommand(agentId), CancellationToken)),
            Operation.Activate => RunWithLoggerAsync<ActivateAgentHandler>(logger =>
                new ActivateAgentHandler(currentUser, _agents, _unitOfWork, logger)
                    .HandleAsync(new ActivateAgentCommand(agentId), CancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
        };

    private Agent AgentCanBeLoaded(Agent agent)
    {
        _agents.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);
        return agent;
    }

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _agents.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
