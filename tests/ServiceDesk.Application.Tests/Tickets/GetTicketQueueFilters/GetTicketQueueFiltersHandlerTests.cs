using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.GetTicketQueueFilters;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.GetTicketQueueFilters;

public sealed class GetTicketQueueFiltersHandlerTests
{
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(new GetTicketQueueFiltersQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetTicketQueueFiltersAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new GetTicketQueueFiltersQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetTicketQueueFiltersAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_Admin_ReturnsCategoriesAndAgentsWithInactiveEntries()
    {
        // Arrange
        var filters = new TicketQueueFiltersDto(
            [new QueueFilterOptionDto(Guid.CreateVersion7(), "Egyéb", IsActive: false)],
            [new QueueFilterOptionDto(Guid.CreateVersion7(), "Kiss Anna", IsActive: true)]);
        _ticketQueries.GetTicketQueueFiltersAsync(Arg.Any<CancellationToken>()).Returns(filters);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new GetTicketQueueFiltersQuery(), CancellationToken);

        // Assert
        result.Value.ShouldBeSameAs(filters);
        await _ticketQueries.Received(1).GetTicketQueueFiltersAsync(CancellationToken);
    }

    private GetTicketQueueFiltersHandler CreateHandler(ICurrentUser currentUser) => new(currentUser, _ticketQueries);
}
