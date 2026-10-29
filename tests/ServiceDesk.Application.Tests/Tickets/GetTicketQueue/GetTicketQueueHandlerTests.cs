using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.GetTicketQueue;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.GetTicketQueue;

public sealed class GetTicketQueueHandlerTests
{
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(new GetTicketQueueQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new GetTicketQueueQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task HandleAsync_AdminWithFilters_QueriesEveryTicketWithPageSize20()
    {
        // Arrange
        var query = new GetTicketQueueQuery(
            PageNumber: 2,
            Status: TicketStatus.Assigned,
            CategoryId: Guid.CreateVersion7(),
            Priority: Priority.High,
            AssigneeAgentId: Guid.CreateVersion7());
        var page = new PagedResult<TicketQueueItemDto>([], 2, GetTicketQueueQuery.PageSize, 0);
        _ticketQueries.GetTicketQueueAsync(query, GetTicketQueueQuery.PageSize, Arg.Any<CancellationToken>()).Returns(page);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(query, CancellationToken);

        // Assert
        result.Value.ShouldBeSameAs(page);
        GetTicketQueueQuery.PageSize.ShouldBe(20);
        await _ticketQueries.Received(1).GetTicketQueueAsync(query, 20, CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_AssigneeAndUnassignedFilterTogether_ReturnsValidationErrorWithoutQuery()
    {
        // Arrange
        var query = new GetTicketQueueQuery(AssigneeAgentId: Guid.CreateVersion7(), UnassignedOnly: true);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(query, CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.AssigneeAgentId");
        result.Error.Type.ShouldBe(ErrorType.Validation);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    public async Task HandleAsync_InvalidPageAndFilters_ReturnsEveryFieldError()
    {
        // Arrange
        var query = new GetTicketQueueQuery(PageNumber: 0, Status: (TicketStatus)99, Priority: (Priority)99);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(query, CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.PageNumber");
        result.Error.FieldErrors.Keys.ShouldBe(
            [nameof(GetTicketQueueQuery.PageNumber), nameof(GetTicketQueueQuery.Status), nameof(GetTicketQueueQuery.Priority)],
            ignoreOrder: true);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task HandleAsync_ClosedStatusWithoutIncludeClosed_ReturnsValidationErrorWithoutQuery()
    {
        // Arrange
        var query = new GetTicketQueueQuery(Status: TicketStatus.Closed);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(query, CancellationToken);

        // Assert
        query.IncludeClosed.ShouldBeFalse();
        result.Error.Code.ShouldBe("Validation.Status");
        result.Error.Type.ShouldBe(ErrorType.Validation);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task HandleAsync_ClosedStatusWithIncludeClosed_IsValid()
    {
        // Arrange
        var query = new GetTicketQueueQuery(Status: TicketStatus.Closed, IncludeClosed: true);
        var page = new PagedResult<TicketQueueItemDto>([], 1, GetTicketQueueQuery.PageSize, 0);
        _ticketQueries.GetTicketQueueAsync(query, GetTicketQueueQuery.PageSize, Arg.Any<CancellationToken>()).Returns(page);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(query, CancellationToken);

        // Assert
        result.Value.ShouldBeSameAs(page);
    }

    [Fact]
    public async Task HandleAsync_UnassignedOnlyWithoutAssignee_IsValid()
    {
        // Arrange
        var query = new GetTicketQueueQuery(UnassignedOnly: true);
        var page = new PagedResult<TicketQueueItemDto>([], 1, GetTicketQueueQuery.PageSize, 0);
        _ticketQueries.GetTicketQueueAsync(query, GetTicketQueueQuery.PageSize, Arg.Any<CancellationToken>()).Returns(page);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(query, CancellationToken);

        // Assert
        result.Value.ShouldBeSameAs(page);
    }

    private GetTicketQueueHandler CreateHandler(ICurrentUser currentUser) =>
        new(currentUser, new GetTicketQueueValidator(), _ticketQueries);

    private async Task ShouldNotHaveQueriedAsync() =>
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetTicketQueueAsync(default!, default, CancellationToken);
}
