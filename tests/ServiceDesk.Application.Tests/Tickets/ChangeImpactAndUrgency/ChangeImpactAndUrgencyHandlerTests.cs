using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.ChangeImpactAndUrgency;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.ChangeImpactAndUrgency;

public sealed class ChangeImpactAndUrgencyHandlerTests
{
    private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly RecordingLogger<ChangeImpactAndUrgencyHandler> _logger = new();
    private readonly Category _category = TestData.CreateCategory();

    public ChangeImpactAndUrgencyHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(Command(Guid.CreateVersion7()), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(Command(ticket.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        ticket.Priority.ShouldBe(Priority.Medium);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_MissingImpact_ReturnsValidationErrorWithoutLoading()
    {
        // Arrange
        var command = new ChangeImpactAndUrgencyCommand(Guid.CreateVersion7(), Impact: null, Urgency.High);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.Code.ShouldBe(TicketErrors.ImpactInvalid.Code);
        result.Error.FieldErrors.Keys.ShouldBe([nameof(ChangeImpactAndUrgencyCommand.Impact)]);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_UnknownTicket_ReturnsNotFound()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(Guid.CreateVersion7()), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    [Trait("BR", "BR-11")]
    public async Task HandleAsync_OpenTicket_RecalculatesPrioritySavesAndLogs()
    {
        // Arrange: Medium × Medium = Medium.
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));
        _time.Advance(TimeSpan.FromMinutes(5));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket.Id), CancellationToken);

        // Assert: High × High = Critical.
        result.IsSuccess.ShouldBeTrue();
        (ticket.Impact, ticket.Urgency, ticket.Priority).ShouldBe((Impact.High, Urgency.High, Priority.Critical));
        ticket.History.Skip(1).Select(entry => (entry.Action, entry.ActorUserId, entry.At)).ShouldBe(
        [
            (TicketHistoryAction.ImpactChanged, TestUsers.UserId, _time.GetUtcNow()),
            (TicketHistoryAction.UrgencyChanged, TestUsers.UserId, _time.GetUtcNow()),
        ]);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Properties["Priority"].ShouldBe(Priority.Critical);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task HandleAsync_ClosedTicket_ReturnsClosedCannotBeModifiedWithoutSave()
    {
        // Arrange
        var agent = TestData.CreateAgent("Kiss Anna", _category);
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.Closed, _category, agent));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.ClosedCannotBeModified);
        result.Error.Message.ShouldBe("Lezárt jegy adatai nem módosíthatók.");
        (ticket.Impact, ticket.Urgency, ticket.Priority).ShouldBe((Impact.Medium, Urgency.Medium, Priority.Medium));
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        _logger.Entries.ShouldBeEmpty();
    }

    private static ChangeImpactAndUrgencyCommand Command(Guid ticketId) => new(ticketId, Impact.High, Urgency.High);

    private ChangeImpactAndUrgencyHandler CreateHandler(ICurrentUser currentUser) =>
        new(currentUser, new ChangeImpactAndUrgencyValidator(), _tickets, _unitOfWork, _time, _logger);

    private Ticket TicketCanBeLoaded(Ticket ticket)
    {
        _tickets.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        return ticket;
    }

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _tickets.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
