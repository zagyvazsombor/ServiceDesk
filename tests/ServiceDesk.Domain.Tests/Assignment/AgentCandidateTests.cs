using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Assignment;

public sealed class AgentCandidateTests
{
    [Fact]
    [Trait("BR", "BR-05")]
    public void Load_NoOpenTickets_ReturnsZero()
    {
        var candidate = new AgentCandidate(Guid.NewGuid(), null, []);

        candidate.Load.ShouldBe(0);
    }

    [Theory]
    [Trait("BR", "BR-05")]
    [InlineData(new[] { Priority.Critical }, 5)]
    [InlineData(new[] { Priority.Low, Priority.Planning }, 3)]
    [InlineData(new[] { Priority.Medium, Priority.Medium }, 6)]
    [InlineData(new[] { Priority.Critical, Priority.High, Priority.Medium, Priority.Low, Priority.Planning }, 15)]
    public void Load_OpenTickets_ReturnsSumOfPriorityWeights(Priority[] openTicketPriorities, int expected)
    {
        var candidate = new AgentCandidate(Guid.NewGuid(), null, openTicketPriorities);

        candidate.Load.ShouldBe(expected);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void Load_OneCritical_IsGreaterThanLowAndPlanningTogether()
    {
        var oneCritical = new AgentCandidate(Guid.NewGuid(), null, [Priority.Critical]);
        var lowAndPlanning = new AgentCandidate(Guid.NewGuid(), null, [Priority.Low, Priority.Planning]);

        oneCritical.Load.ShouldBeGreaterThan(lowAndPlanning.Load);
    }

    [Fact]
    public void Constructor_NullOpenTicketPriorities_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new AgentCandidate(Guid.NewGuid(), null, null!));
    }
}
