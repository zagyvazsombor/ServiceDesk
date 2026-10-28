using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Assignment;

// Each case is arranged so that the keys after the deciding one would pick a different agent.
public sealed class LeastLoadedAssignmentStrategyTests
{
    private static readonly Guid AgentId1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid AgentId2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid AgentId3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid AgentId4 = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private readonly LeastLoadedAssignmentStrategy _strategy = new();

    [Fact]
    [Trait("BR", "BR-05")]
    public void SelectAgent_DifferentLoads_ReturnsLeastLoadedAgent()
    {
        AgentCandidate[] candidates =
        [
            Candidate(AgentId1, null, Priority.High),
            Candidate(AgentId2, TestData.Now, Priority.Medium, Priority.Low),
            Candidate(AgentId3, TestData.Later, Priority.Medium),
        ];

        var selected = _strategy.SelectAgent(candidates);

        selected.ShouldBe(AgentId3);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void SelectAgent_OneCriticalVersusLowAndPlanning_ReturnsAgentWithLowAndPlanning()
    {
        AgentCandidate[] candidates =
        [
            Candidate(AgentId1, null, Priority.Critical),
            Candidate(AgentId2, TestData.Later, Priority.Low, Priority.Planning),
        ];

        var selected = _strategy.SelectAgent(candidates);

        selected.ShouldBe(AgentId2);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void SelectAgent_EqualLoad_ReturnsAgentWithOldestLastAssignedAt()
    {
        AgentCandidate[] candidates =
        [
            Candidate(AgentId1, TestData.Later, Priority.Medium),
            Candidate(AgentId2, TestData.Now, Priority.Low, Priority.Planning),
        ];

        var selected = _strategy.SelectAgent(candidates);

        selected.ShouldBe(AgentId2);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public void SelectAgent_EqualLoadAndOneNeverAssigned_ReturnsNeverAssignedAgent()
    {
        AgentCandidate[] candidates =
        [
            Candidate(AgentId1, DateTimeOffset.MinValue, Priority.Medium),
            Candidate(AgentId2, null, Priority.Medium),
        ];

        var selected = _strategy.SelectAgent(candidates);

        selected.ShouldBe(AgentId2);
    }

    [Theory]
    [Trait("BR", "BR-05")]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectAgent_EqualLoadAndLastAssignedAt_ReturnsSmallestAgentId(bool neverAssigned)
    {
        DateTimeOffset? lastAssignedAt = neverAssigned ? null : TestData.Now;
        AgentCandidate[] candidates =
        [
            Candidate(AgentId3, lastAssignedAt, Priority.Medium),
            Candidate(AgentId1, lastAssignedAt, Priority.Low, Priority.Planning),
            Candidate(AgentId2, lastAssignedAt, Priority.Medium),
        ];

        var selected = _strategy.SelectAgent(candidates);

        selected.ShouldBe(AgentId1);
    }

    [Theory]
    [Trait("BR", "BR-05")]
    [InlineData(new[] { 0, 1, 2, 3 })]
    [InlineData(new[] { 3, 2, 1, 0 })]
    [InlineData(new[] { 2, 0, 3, 1 })]
    [InlineData(new[] { 1, 3, 0, 2 })]
    public void SelectAgent_AnyInputOrder_ReturnsSameAgent(int[] inputOrder)
    {
        // AgentId3 wins: AgentId1 has more load, AgentId2 was assigned, AgentId4 has a larger id.
        AgentCandidate[] candidates =
        [
            Candidate(AgentId1, null, Priority.High),
            Candidate(AgentId2, TestData.Now, Priority.Medium),
            Candidate(AgentId3, null, Priority.Medium),
            Candidate(AgentId4, null, Priority.Low, Priority.Planning),
        ];

        var selected = _strategy.SelectAgent([.. inputOrder.Select(index => candidates[index])]);

        selected.ShouldBe(AgentId3);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public void SelectAgent_NoCandidates_ReturnsNull()
    {
        var selected = _strategy.SelectAgent([]);

        selected.ShouldBeNull();
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public void SelectAgent_SingleCandidate_ReturnsThatAgent()
    {
        AgentCandidate[] candidates = [Candidate(AgentId1, TestData.Now, Priority.Critical, Priority.Critical)];

        var selected = _strategy.SelectAgent(candidates);

        selected.ShouldBe(AgentId1);
    }

    [Fact]
    public void SelectAgent_NullCandidates_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _strategy.SelectAgent(null!));
    }

    private static AgentCandidate Candidate(
        Guid agentId,
        DateTimeOffset? lastAssignedAt,
        params Priority[] openTicketPriorities) =>
        new(agentId, lastAssignedAt, openTicketPriorities);
}
