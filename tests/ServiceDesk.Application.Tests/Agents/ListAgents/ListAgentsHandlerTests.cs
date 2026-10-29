using NSubstitute;
using ServiceDesk.Application.Agents.ListAgents;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using Shouldly;

namespace ServiceDesk.Application.Tests.Agents.ListAgents;

public sealed class ListAgentsHandlerTests
{
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(new ListAgentsQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetAgentsAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new ListAgentsQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetAgentsAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public async Task HandleAsync_Admin_ReturnsAgentsWithInactiveOnes()
    {
        // Arrange
        var hardware = new AgentCompetencyDto(Guid.CreateVersion7(), "HARDWARE", "Hardver", IsActive: true);
        IReadOnlyList<AgentListItemDto> agents =
        [
            new(Guid.CreateVersion7(), "Kiss Anna", IsActive: true, [hardware], Load: 7, OpenTicketCount: 2),
            new(Guid.CreateVersion7(), "Nagy Béla", IsActive: false, [], Load: 4, OpenTicketCount: 1),
        ];
        _ticketQueries.GetAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new ListAgentsQuery(), CancellationToken);

        // Assert
        result.Value.ShouldBeSameAs(agents);
        await _ticketQueries.Received(1).GetAgentsAsync(CancellationToken);
    }

    private ListAgentsHandler CreateHandler(ICurrentUser currentUser) => new(currentUser, _ticketQueries);
}
