using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.GetTicketDetails;

public sealed class GetTicketDetailsHandlerTests
{
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();
    private readonly RecordingLogger<GetTicketDetailsHandler> _logger = new();
    private readonly Guid _ticketId = Guid.CreateVersion7();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(new GetTicketDetailsQuery(_ticketId), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    public async Task HandleAsync_SignedInUserWithoutRole_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.SignedIn()).HandleAsync(new GetTicketDetailsQuery(_ticketId), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveQueriedAsync();
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task HandleAsync_ReporterForeignTicket_ReturnsNotFoundAndLogsWarningWithoutContent()
    {
        // Arrange: with the reporter filter the query does not find a ticket of another reporter.
        DetailsAre(reporterId: TestUsers.UserId, details: null);

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new GetTicketDetailsQuery(_ticketId), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound);
        await _ticketQueries.Received(1).GetTicketDetailsAsync(_ticketId, TestUsers.UserId, CancellationToken);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["UserId"].ShouldBe(TestUsers.UserId);
        entry.Properties["TicketId"].ShouldBe(_ticketId);
        entry.Properties.Keys.ShouldBe(["UserId", "TicketId", "{OriginalFormat}"], ignoreOrder: true);
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task HandleAsync_ReporterOwnTicket_ReturnsDetailsWithoutLog()
    {
        // Arrange
        var details = Details();
        DetailsAre(reporterId: TestUsers.UserId, details);

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new GetTicketDetailsQuery(_ticketId), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(details);
        await _ticketQueries.Received(1).GetTicketDetailsAsync(_ticketId, TestUsers.UserId, CancellationToken);
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task HandleAsync_Admin_QueriesWithoutReporterFilter()
    {
        // Arrange
        var details = Details();
        DetailsAre(reporterId: null, details);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new GetTicketDetailsQuery(_ticketId), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(details);
        await _ticketQueries.Received(1).GetTicketDetailsAsync(_ticketId, null, CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_AdminMissingTicket_ReturnsNotFoundWithoutLog()
    {
        // Arrange
        DetailsAre(reporterId: null, details: null);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new GetTicketDetailsQuery(_ticketId), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound);
        _logger.Entries.ShouldBeEmpty();
    }

    private GetTicketDetailsHandler CreateHandler(ICurrentUser currentUser) => new(currentUser, _ticketQueries, _logger);

    private void DetailsAre(string? reporterId, TicketDetailsDto? details) =>
        _ticketQueries.GetTicketDetailsAsync(_ticketId, reporterId, Arg.Any<CancellationToken>()).Returns(details);

    private TicketDetailsDto Details() => new(
        _ticketId,
        TestData.ValidTitle,
        TestData.ValidDescription,
        Impact.Medium,
        Urgency.Medium,
        Priority.Medium,
        TicketStatus.New,
        TestData.Now,
        "bejelento.dora@servicedesk.local",
        "HARDWARE",
        "Hardver",
        AssigneeName: null,
        [new TicketHistoryEntryDto(1, TestData.Now, TicketHistoryAction.Created, "bejelento.dora@servicedesk.local", null, "New")]);

    private Task<TicketDetailsDto?> ShouldNotHaveQueriedAsync() =>
        _ticketQueries.DidNotReceiveWithAnyArgs().GetTicketDetailsAsync(default, default, CancellationToken);
}
