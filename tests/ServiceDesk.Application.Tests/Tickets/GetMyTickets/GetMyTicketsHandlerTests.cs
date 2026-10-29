using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.GetMyTickets;

// The query has no ticket Id: the list is always read for the signed-in Reporter, so there is no "foreign ticket" case.
public sealed class GetMyTicketsHandlerTests
{
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(new GetMyTicketsQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    public async Task HandleAsync_Admin_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new GetMyTicketsQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task HandleAsync_Reporter_QueriesOwnTicketsWithPageSize20()
    {
        // Arrange
        var page = new PagedResult<MyTicketDto>(
            [new MyTicketDto(Guid.CreateVersion7(), TestData.ValidTitle, "Hardver", Priority.Medium, TicketStatus.New, TestData.Now)],
            PageNumber: 2,
            PageSize: 20,
            TotalCount: 21);
        _ticketQueries.GetMyTicketsAsync(TestUsers.UserId, 2, 20, Arg.Any<CancellationToken>()).Returns(page);

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new GetMyTicketsQuery(PageNumber: 2), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(page);
        await _ticketQueries.Received(1).GetMyTicketsAsync(TestUsers.UserId, 2, GetMyTicketsQuery.PageSize, CancellationToken);
        GetMyTicketsQuery.PageSize.ShouldBe(20);
    }

    [Fact]
    public async Task HandleAsync_PageNumberZero_ReturnsValidationErrorWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new GetMyTicketsQuery(PageNumber: 0), CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.PageNumber");
        result.Error.FieldErrors.Keys.ShouldBe([nameof(GetMyTicketsQuery.PageNumber)]);
        await ShouldNotHaveQueriedAsync();
    }

    private GetMyTicketsHandler CreateHandler(ICurrentUser currentUser) =>
        new(currentUser, new GetMyTicketsValidator(), _ticketQueries);

    private Task<PagedResult<MyTicketDto>> ShouldNotHaveQueriedAsync() =>
        _ticketQueries.DidNotReceiveWithAnyArgs().GetMyTicketsAsync(default!, default, default, CancellationToken);
}
