using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.GetAssignableAgents;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.GetAssignableAgents;

public sealed class GetAssignableAgentsHandlerTests
{
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();
    private readonly Guid _ticketId = Guid.CreateVersion7();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(new GetAssignableAgentsQuery(_ticketId), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetAssignableAgentsAsync(default, CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new GetAssignableAgentsQuery(_ticketId), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetAssignableAgentsAsync(default, CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_UnknownTicket_ReturnsNotFound()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new GetAssignableAgentsQuery(_ticketId), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task HandleAsync_ClosedTicket_ReturnsClosedCannotBeAssigned()
    {
        // Arrange
        AssignableAgentsAre(new AssignableAgentsDto(TicketStatus.Closed, [Agent("Kiss Anna", isCurrentAssignee: true)]));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new GetAssignableAgentsQuery(_ticketId), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.ClosedCannotBeAssigned);
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [Trait("BR", "BR-04")]
    public async Task HandleAsync_OpenTicket_ReturnsAssignableAgents(TicketStatus status)
    {
        // Arrange
        var assignableAgents = new AssignableAgentsDto(status, [Agent("Nagy Béla"), Agent("Kiss Anna", load: 5)]);
        AssignableAgentsAre(assignableAgents);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new GetAssignableAgentsQuery(_ticketId), CancellationToken);

        // Assert
        result.Value.ShouldBeSameAs(assignableAgents);
        await _ticketQueries.Received(1).GetAssignableAgentsAsync(_ticketId, CancellationToken);
    }

    private static AssignableAgentDto Agent(string displayName, int load = 0, bool isCurrentAssignee = false) =>
        new(Guid.CreateVersion7(), displayName, load, OpenTicketCount: load == 0 ? 0 : 1, isCurrentAssignee);

    private void AssignableAgentsAre(AssignableAgentsDto assignableAgents) =>
        _ticketQueries.GetAssignableAgentsAsync(_ticketId, Arg.Any<CancellationToken>()).Returns(assignableAgents);

    private GetAssignableAgentsHandler CreateHandler(ICurrentUser currentUser) => new(currentUser, _ticketQueries);
}
