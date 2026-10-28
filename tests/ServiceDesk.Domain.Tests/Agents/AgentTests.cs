using ServiceDesk.Domain.Agents;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Agents;

public sealed class AgentTests
{
    private const string UserId = "user-anna";

    [Fact]
    public void Create_ValidInput_ReturnsActiveAgentWithoutCompetencies()
    {
        var result = Agent.Create(UserId, "  Kiss Anna ");

        result.IsSuccess.ShouldBeTrue();
        var agent = result.Value;
        agent.Id.ShouldNotBe(Guid.Empty);
        agent.UserId.ShouldBe(UserId);
        agent.DisplayName.ShouldBe("Kiss Anna");
        agent.IsActive.ShouldBeTrue();
        agent.LastAssignedAt.ShouldBeNull();
        agent.Competencies.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Agent.DisplayNameMinLength)]
    [InlineData(Agent.DisplayNameMaxLength)]
    public void Create_DisplayNameLengthAtLimit_Succeeds(int length)
    {
        var result = Agent.Create(UserId, new string('a', length));

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(Agent.DisplayNameMinLength - 1)]
    [InlineData(Agent.DisplayNameMaxLength + 1)]
    public void Create_DisplayNameLengthOutOfRange_ReturnsDisplayNameLengthError(int length)
    {
        var result = Agent.Create(UserId, new string('a', length));

        result.Error.ShouldBe(AgentErrors.DisplayNameLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingUserId_ThrowsArgumentException(string userId)
    {
        Should.Throw<ArgumentException>(() => Agent.Create(userId, "Kiss Anna"));
    }

    [Fact]
    public void SetCompetencies_NewCategoryIds_ReplacesCompetencySet()
    {
        var hardware = Guid.CreateVersion7();
        var network = Guid.CreateVersion7();
        var software = Guid.CreateVersion7();
        var agent = Agent.Create(UserId, "Kiss Anna").Value;
        agent.SetCompetencies([hardware, network]);

        var result = agent.SetCompetencies([network, software]);

        result.IsSuccess.ShouldBeTrue();
        agent.HasCompetency(hardware).ShouldBeFalse();
        agent.HasCompetency(network).ShouldBeTrue();
        agent.HasCompetency(software).ShouldBeTrue();
        agent.Competencies.ShouldAllBe(competency => competency.AgentId == agent.Id);
    }

    [Fact]
    public void SetCompetencies_DuplicateCategoryIds_StoresEachOnce()
    {
        var hardware = Guid.CreateVersion7();
        var agent = Agent.Create(UserId, "Kiss Anna").Value;

        agent.SetCompetencies([hardware, hardware]);

        agent.Competencies.ShouldHaveSingleItem().CategoryId.ShouldBe(hardware);
    }

    [Fact]
    public void SetCompetencies_EmptyList_ClearsCompetencies()
    {
        var agent = Agent.Create(UserId, "Kiss Anna").Value;
        agent.SetCompetencies([Guid.CreateVersion7()]);

        var result = agent.SetCompetencies([]);

        result.IsSuccess.ShouldBeTrue();
        agent.Competencies.ShouldBeEmpty();
    }

    [Fact]
    public void SetCompetencies_EmptyGuid_ReturnsErrorAndKeepsCompetencies()
    {
        var hardware = Guid.CreateVersion7();
        var agent = Agent.Create(UserId, "Kiss Anna").Value;
        agent.SetCompetencies([hardware]);

        var result = agent.SetCompetencies([Guid.CreateVersion7(), Guid.Empty]);

        result.Error.ShouldBe(AgentErrors.CompetencyCategoryInvalid);
        agent.Competencies.ShouldHaveSingleItem().CategoryId.ShouldBe(hardware);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public void Deactivate_ActiveAgent_BecomesInactiveAndKeepsCompetencies()
    {
        var hardware = Guid.CreateVersion7();
        var agent = Agent.Create(UserId, "Kiss Anna").Value;
        agent.SetCompetencies([hardware]);

        agent.Deactivate();

        agent.IsActive.ShouldBeFalse();
        agent.HasCompetency(hardware).ShouldBeTrue();
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public void Activate_InactiveAgent_BecomesActive()
    {
        var agent = Agent.Create(UserId, "Kiss Anna").Value;
        agent.Deactivate();

        agent.Activate();

        agent.IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void RecordAssignment_Now_SetsLastAssignedAt()
    {
        var agent = Agent.Create(UserId, "Kiss Anna").Value;

        agent.RecordAssignment(TestData.Now);

        agent.LastAssignedAt.ShouldBe(TestData.Now);
    }
}
