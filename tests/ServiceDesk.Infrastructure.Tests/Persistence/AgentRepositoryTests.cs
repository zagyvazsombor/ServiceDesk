using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

public sealed class AgentRepositoryTests(DatabaseFixture database) : IntegrationTest(database)
{
    [Fact]
    public async Task GetByIdAsync_SavedAgent_ReturnsAgentWithCompetencies()
    {
        // Arrange
        var user = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var agent = new AgentBuilder(user).WithCompetencies(hardware, network).Build();
        await SaveAsync(user, hardware, network, agent);

        // Act
        await using var scope = Database.CreateScope();
        var loaded = await scope.ServiceProvider.GetRequiredService<IAgentRepository>()
            .GetByIdAsync(agent.Id, CancellationToken);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.UserId.ShouldBe(user.Id);
        loaded.DisplayName.ShouldBe(agent.DisplayName);
        loaded.IsActive.ShouldBeTrue();
        loaded.LastAssignedAt.ShouldBeNull();
        loaded.Competencies.ShouldAllBe(competency => competency.AgentId == agent.Id);
        loaded.Competencies.Select(competency => competency.CategoryId).ShouldBe([hardware.Id, network.Id], ignoreOrder: true);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task GetEligibleCandidatesAsync_MixedAgents_ReturnsOnlyActiveCompetentAgents()
    {
        // Arrange
        var users = new[] { new UserBuilder().Build(), new UserBuilder().Build(), new UserBuilder().Build() };
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var activeCompetent = new AgentBuilder(users[0]).WithCompetencies(hardware).Build();
        var inactiveCompetent = new AgentBuilder(users[1]).WithCompetencies(hardware).Inactive().Build();
        var activeWithoutCompetency = new AgentBuilder(users[2]).WithCompetencies(network).Build();
        await SaveAsync([.. users, hardware, network, activeCompetent, inactiveCompetent, activeWithoutCompetency]);

        // Act
        var candidates = await GetEligibleCandidatesAsync(hardware.Id);

        // Assert
        candidates.Select(candidate => candidate.AgentId).ShouldBe([activeCompetent.Id]);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task GetEligibleCandidatesAsync_NoActiveCompetentAgent_ReturnsEmptyList()
    {
        // Arrange
        var users = new[] { new UserBuilder().Build(), new UserBuilder().Build() };
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var inactiveCompetent = new AgentBuilder(users[0]).WithCompetencies(hardware).Inactive().Build();
        var activeWithoutCompetency = new AgentBuilder(users[1]).WithCompetencies(network).Build();
        await SaveAsync([.. users, hardware, network, inactiveCompetent, activeWithoutCompetency]);

        // Act
        var candidates = await GetEligibleCandidatesAsync(hardware.Id);

        // Assert
        candidates.ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public async Task GetEligibleCandidatesAsync_TicketsInEveryStatus_ReturnsOnlyOpenTicketPriorities()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var admin = new UserBuilder().Build();
        var otherAdmin = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var agent = new AgentBuilder(admin).WithCompetencies(hardware, network).Build();
        var otherAgent = new AgentBuilder(otherAdmin).WithCompetencies(hardware).Build();
        var tickets = new[]
        {
            // Open: they count, whatever their category.
            new TicketBuilder(reporter, hardware).WithImpactAndUrgency(Impact.High, Urgency.Medium) // High
                .InStatus(TicketStatus.Assigned, agent, admin).Build(),
            new TicketBuilder(reporter, hardware).WithImpactAndUrgency(Impact.High, Urgency.High) // Critical
                .InStatus(TicketStatus.InProgress, agent, admin).Build(),
            new TicketBuilder(reporter, network).WithImpactAndUrgency(Impact.Low, Urgency.Low) // Planning
                .InStatus(TicketStatus.Assigned, agent, admin).Build(),
            // Not open: they do not count.
            new TicketBuilder(reporter, hardware).WithImpactAndUrgency(Impact.Medium, Urgency.Medium) // Medium
                .InStatus(TicketStatus.Resolved, agent, admin).Build(),
            new TicketBuilder(reporter, hardware).WithImpactAndUrgency(Impact.Medium, Urgency.Low) // Low
                .InStatus(TicketStatus.Closed, agent, admin).Build(),
            // Open, but assigned to the other agent.
            new TicketBuilder(reporter, hardware).WithImpactAndUrgency(Impact.Medium, Urgency.Medium) // Medium
                .InStatus(TicketStatus.Assigned, otherAgent, admin).Build(),
        };
        await SaveAsync([reporter, admin, otherAdmin, hardware, network, agent, otherAgent, .. tickets]);

        // Act
        var candidates = await GetEligibleCandidatesAsync(hardware.Id);

        // Assert
        candidates.Count.ShouldBe(2);
        var candidate = candidates.Single(candidate => candidate.AgentId == agent.Id);
        candidate.OpenTicketPriorities.ShouldBe([Priority.High, Priority.Critical, Priority.Planning], ignoreOrder: true);
        candidate.Load.ShouldBe(10); // 4 + 5 + 1
        candidate.LastAssignedAt.ShouldBe(agent.LastAssignedAt);
        var otherCandidate = candidates.Single(candidate => candidate.AgentId == otherAgent.Id);
        otherCandidate.OpenTicketPriorities.ShouldBe([Priority.Medium]);
        otherCandidate.Load.ShouldBe(3);
    }

    private async Task<IReadOnlyList<AgentCandidate>> GetEligibleCandidatesAsync(Guid categoryId)
    {
        await using var scope = Database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAgentRepository>()
            .GetEligibleCandidatesAsync(categoryId, CancellationToken);
    }
}
