using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Tickets;

public sealed class TicketAssignmentTests
{
    private readonly Category _category = TestData.CreateCategory("HARDWARE");
    private readonly Category _otherCategory = TestData.CreateCategory("NETWORK");

    [Fact]
    [Trait("BR", "BR-04")]
    public void AssignTo_ActiveAgentWithCompetency_AssignsTicket()
    {
        var ticket = TestData.CreateTicket(_category);
        var agent = TestData.CreateAgent(_category);

        var result = ticket.AssignTo(agent, null, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.AssigneeAgentId.ShouldBe(agent.Id);
        ticket.Status.ShouldBe(TicketStatus.Assigned);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public void AssignTo_AgentWithoutCompetency_ReturnsMissingCompetencyError()
    {
        var ticket = TestData.CreateTicket(_category);
        var agent = TestData.CreateAgent(_otherCategory);

        var result = ticket.AssignTo(agent, null, TestData.Later);

        result.Error.ShouldBe(AgentErrors.MissingCompetency);
        ticket.AssigneeAgentId.ShouldBeNull();
        ticket.Status.ShouldBe(TicketStatus.New);
        agent.LastAssignedAt.ShouldBeNull();
    }

    [Fact]
    [Trait("BR", "BR-04")]
    [Trait("BR", "BR-12")]
    public void AssignTo_InactiveAgent_ReturnsAgentInactiveError()
    {
        var ticket = TestData.CreateTicket(_category);
        var agent = TestData.CreateAgent(_category);
        agent.Deactivate();

        var result = ticket.AssignTo(agent, TestData.AdminUserId, TestData.Later);

        result.Error.ShouldBe(AgentErrors.Inactive);
        ticket.AssigneeAgentId.ShouldBeNull();
        ticket.Status.ShouldBe(TicketStatus.New);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public void Deactivate_AgentWithAssignedTicket_TicketKeepsAssigneeAndCanProgress()
    {
        var agent = TestData.CreateAgent(_category);
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Assigned, _category, agent);

        agent.Deactivate();
        var result = ticket.Start(TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.AssigneeAgentId.ShouldBe(agent.Id);
        ticket.Status.ShouldBe(TicketStatus.InProgress);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public void AssignTo_TicketOfInactiveAgentToActiveAgent_ReassignsTicket()
    {
        var inactiveAgent = TestData.CreateAgent(_category);
        var ticket = TestData.CreateTicketInStatus(TicketStatus.InProgress, _category, inactiveAgent);
        inactiveAgent.Deactivate();
        var activeAgent = TestData.CreateAgent(_category);

        var result = ticket.AssignTo(activeAgent, TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.AssigneeAgentId.ShouldBe(activeAgent.Id);
        ticket.Status.ShouldBe(TicketStatus.InProgress);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public void AssignTo_ClosedTicket_ReturnsClosedCannotBeAssignedError()
    {
        var agent = TestData.CreateAgent(_category);
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Closed, _category, agent);
        var otherAgent = TestData.CreateAgent(_category);

        var result = ticket.AssignTo(otherAgent, TestData.AdminUserId, TestData.Later);

        result.Error.ShouldBe(TicketErrors.ClosedCannotBeAssigned);
        ticket.AssigneeAgentId.ShouldBe(agent.Id);
        otherAgent.LastAssignedAt.ShouldBeNull();
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public void AssignTo_ClosedTicketAndInactiveAgent_ReturnsClosedErrorFirst()
    {
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Closed, _category, TestData.CreateAgent(_category));
        var inactiveAgent = TestData.CreateAgent(_otherCategory);
        inactiveAgent.Deactivate();

        var result = ticket.AssignTo(inactiveAgent, TestData.AdminUserId, TestData.Later);

        result.Error.ShouldBe(TicketErrors.ClosedCannotBeAssigned);
    }

    [Theory]
    [Trait("BR", "BR-03")]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    public void AssignTo_NotClosedTicketWithAssignee_ReassignsWithoutStatusChange(TicketStatus status)
    {
        var ticket = TestData.CreateTicketInStatus(status, _category, TestData.CreateAgent(_category));
        var otherAgent = TestData.CreateAgent(_category);

        var result = ticket.AssignTo(otherAgent, TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.AssigneeAgentId.ShouldBe(otherAgent.Id);
        ticket.Status.ShouldBe(status);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void AssignTo_EligibleAgent_RecordsAssignmentTimeOnAgent()
    {
        var ticket = TestData.CreateTicket(_category);
        var agent = TestData.CreateAgent(_category);

        ticket.AssignTo(agent, null, TestData.Later);

        agent.LastAssignedAt.ShouldBe(TestData.Later);
    }

    [Fact]
    public void AssignTo_CurrentAssignee_ChangesNothing()
    {
        var agent = TestData.CreateAgent(_category);
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Assigned, _category, agent);
        var historyCountBefore = ticket.History.Count;

        var result = ticket.AssignTo(agent, TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.History.Count.ShouldBe(historyCountBefore);
        agent.LastAssignedAt.ShouldBe(TestData.Now);
    }

    [Fact]
    public void AssignTo_NullAgent_ThrowsArgumentNullException()
    {
        var ticket = TestData.CreateTicket(_category);

        Should.Throw<ArgumentNullException>(() => ticket.AssignTo(null!, null, TestData.Later));
    }
}
