using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Tickets;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets;

public sealed class TicketAssignmentServiceTests
{
    private readonly IAgentRepository _agents = Substitute.For<IAgentRepository>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly Category _category = TestData.CreateCategory();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("BR", "BR-05")]
    public async Task AutoAssignAsync_SeveralCandidates_AssignsLeastLoadedAgentAsSystem()
    {
        // Arrange
        var busyAgent = TestData.CreateAgent("Kiss Anna", _category);
        var freeAgent = TestData.CreateAgent("Nagy Béla", _category);
        CandidatesAre(TestData.CandidateOf(busyAgent, Priority.Critical), TestData.CandidateOf(freeAgent, Priority.Low));
        AgentsCanBeLoaded(busyAgent, freeAgent);
        var ticket = TestData.CreateTicket(_category);
        _time.Advance(TimeSpan.FromMinutes(5));

        // Act
        var result = await CreateService().AutoAssignAsync(ticket, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(freeAgent.Id);
        ticket.Status.ShouldBe(TicketStatus.Assigned);
        ticket.AssigneeAgentId.ShouldBe(freeAgent.Id);
        var assignment = ticket.History.Single(entry => entry.Action == TicketHistoryAction.Assigned);
        assignment.ActorUserId.ShouldBeNull();
        assignment.At.ShouldBe(_time.GetUtcNow());
        freeAgent.LastAssignedAt.ShouldBe(_time.GetUtcNow());
        busyAgent.LastAssignedAt.ShouldBeNull();
        await _agents.Received(1).GetEligibleCandidatesAsync(_category.Id, CancellationToken);
        await _agents.DidNotReceive().GetByIdAsync(busyAgent.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task AutoAssignAsync_NoCandidate_ReturnsNullAndLeavesTicketNewWithoutAssignee()
    {
        // Arrange
        CandidatesAre();
        var ticket = TestData.CreateTicket(_category);

        // Act
        var result = await CreateService().AutoAssignAsync(ticket, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        ticket.Status.ShouldBe(TicketStatus.New);
        ticket.AssigneeAgentId.ShouldBeNull();
        ticket.History.ShouldHaveSingleItem().Action.ShouldBe(TicketHistoryAction.Created);
        await _agents.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task AutoAssignAsync_LoadedAgentWithoutCompetency_ReturnsMissingCompetency()
    {
        // Arrange: the candidate list is out of date, the loaded agent no longer has the category.
        var agent = TestData.CreateAgent("Kiss Anna", TestData.CreateCategory("NETWORK"));
        CandidatesAre(TestData.CandidateOf(agent));
        AgentsCanBeLoaded(agent);
        var ticket = TestData.CreateTicket(_category);

        // Act
        var result = await CreateService().AutoAssignAsync(ticket, CancellationToken);

        // Assert
        result.Error.ShouldBe(AgentErrors.MissingCompetency);
        ticket.Status.ShouldBe(TicketStatus.New);
        ticket.AssigneeAgentId.ShouldBeNull();
        agent.LastAssignedAt.ShouldBeNull();
    }

    [Fact]
    public async Task AutoAssignAsync_SelectedAgentNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var agent = TestData.CreateAgent("Kiss Anna", _category);
        CandidatesAre(TestData.CandidateOf(agent));
        var ticket = TestData.CreateTicket(_category);

        // Act
        var act = () => CreateService().AutoAssignAsync(ticket, CancellationToken);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
    }

    private TicketAssignmentService CreateService() => new(_agents, new LeastLoadedAssignmentStrategy(), _time);

    private void CandidatesAre(params AgentCandidate[] candidates) =>
        _agents.GetEligibleCandidatesAsync(_category.Id, Arg.Any<CancellationToken>()).Returns(candidates);

    private void AgentsCanBeLoaded(params Agent[] agents)
    {
        foreach (var agent in agents)
        {
            _agents.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);
        }
    }
}
