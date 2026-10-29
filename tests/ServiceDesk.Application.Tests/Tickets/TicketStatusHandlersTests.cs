using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.CloseTicket;
using ServiceDesk.Application.Tickets.ReopenTicket;
using ServiceDesk.Application.Tickets.ResolveTicket;
using ServiceDesk.Application.Tickets.StartWork;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets;

// StartWork, ResolveTicket, ReopenTicket and CloseTicket differ only in the transition they run (AGENTS.md §10).
public sealed class TicketStatusHandlersTests
{
    private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly Category _category = TestData.CreateCategory();
    private readonly Agent _agent;

    public TicketStatusHandlersTests()
    {
        _agent = TestData.CreateAgent("Kiss Anna", _category);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    public enum Operation
    {
        StartWork,
        ResolveTicket,
        ReopenTicket,
        CloseTicket,
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(Operation.StartWork)]
    [InlineData(Operation.ResolveTicket)]
    [InlineData(Operation.ReopenTicket)]
    [InlineData(Operation.CloseTicket)]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(Operation operation)
    {
        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Anonymous(), Guid.CreateVersion7());

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Theory]
    [InlineData(Operation.StartWork)]
    [InlineData(Operation.ResolveTicket)]
    [InlineData(Operation.ReopenTicket)]
    [InlineData(Operation.CloseTicket)]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(Operation operation)
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.Resolved, _category, _agent));

        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Reporter(), ticket.Id);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        ticket.Status.ShouldBe(TicketStatus.Resolved);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Theory]
    [InlineData(Operation.StartWork)]
    [InlineData(Operation.ResolveTicket)]
    [InlineData(Operation.ReopenTicket)]
    [InlineData(Operation.CloseTicket)]
    public async Task HandleAsync_UnknownTicket_ReturnsNotFound(Operation operation)
    {
        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Admin(), Guid.CreateVersion7());

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Theory]
    [InlineData(Operation.StartWork, TicketStatus.Assigned, TicketStatus.InProgress)]
    [InlineData(Operation.ResolveTicket, TicketStatus.InProgress, TicketStatus.Resolved)]
    [InlineData(Operation.ReopenTicket, TicketStatus.Resolved, TicketStatus.InProgress)]
    [InlineData(Operation.CloseTicket, TicketStatus.Resolved, TicketStatus.Closed)]
    [Trait("BR", "BR-10")]
    public async Task HandleAsync_TicketInSourceStatus_ChangesStatusSavesAndLogs(
        Operation operation,
        TicketStatus from,
        TicketStatus to)
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(from, _category, _agent));
        _time.Advance(TimeSpan.FromMinutes(5));

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), ticket.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ticket.Status.ShouldBe(to);
        ticket.AssigneeAgentId.ShouldBe(_agent.Id);
        var entry = ticket.History.Last();
        entry.Action.ShouldBe(TicketHistoryAction.StatusChanged);
        (entry.OldValue, entry.NewValue).ShouldBe((from.ToString(), to.ToString()));
        entry.ActorUserId.ShouldBe(TestUsers.UserId);
        entry.At.ShouldBe(_time.GetUtcNow());
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        var logEntry = log.ShouldHaveSingleItem();
        logEntry.Level.ShouldBe(LogLevel.Information);
        logEntry.Properties["TicketId"].ShouldBe(ticket.Id);
        logEntry.Properties["ActorUserId"].ShouldBe(TestUsers.UserId);
    }

    [Theory]
    [InlineData(Operation.StartWork, TicketStatus.New)]
    [InlineData(Operation.StartWork, TicketStatus.Resolved)]
    [InlineData(Operation.ResolveTicket, TicketStatus.Assigned)]
    [InlineData(Operation.ReopenTicket, TicketStatus.InProgress)]
    [InlineData(Operation.CloseTicket, TicketStatus.InProgress)]
    public async Task HandleAsync_TicketInOtherStatus_ReturnsInvalidTransitionWithoutSave(
        Operation operation,
        TicketStatus status)
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(status, _category, _agent));
        var historyCount = ticket.History.Count;

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), ticket.Id);

        // Assert
        result.Error.ShouldBe(TicketErrors.InvalidTransition);
        result.Error.Message.ShouldBe("A jegy jelenlegi állapotában ez a művelet nem hajtható végre.");
        ticket.Status.ShouldBe(status);
        ticket.History.Count.ShouldBe(historyCount);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
        log.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Operation.StartWork)]
    [InlineData(Operation.ResolveTicket)]
    [InlineData(Operation.ReopenTicket)]
    [InlineData(Operation.CloseTicket)]
    [Trait("BR", "BR-03")]
    public async Task HandleAsync_ClosedTicket_ReturnsInvalidTransitionWithoutSave(Operation operation)
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.Closed, _category, _agent));
        var historyCount = ticket.History.Count;

        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Admin(), ticket.Id);

        // Assert
        result.Error.ShouldBe(TicketErrors.InvalidTransition);
        ticket.Status.ShouldBe(TicketStatus.Closed);
        ticket.History.Count.ShouldBe(historyCount);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Theory]
    [InlineData(Operation.StartWork, TicketStatus.Assigned)]
    [InlineData(Operation.ResolveTicket, TicketStatus.InProgress)]
    [InlineData(Operation.ReopenTicket, TicketStatus.Resolved)]
    [InlineData(Operation.CloseTicket, TicketStatus.Resolved)]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog(
        Operation operation,
        TicketStatus from)
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(from, _category, _agent));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), ticket.Id);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        result.Error.Message.ShouldBe("A jegyet időközben módosították, frissítsd az oldalt.");
        log.ShouldBeEmpty();
    }

    private static async Task<(Result Result, IReadOnlyList<LogEntry> Log)> RunWithLoggerAsync<THandler>(
        Func<RecordingLogger<THandler>, Task<Result>> run)
    {
        var logger = new RecordingLogger<THandler>();
        var result = await run(logger);
        return (result, logger.Entries);
    }

    private Task<(Result Result, IReadOnlyList<LogEntry> Log)> RunAsync(
        Operation operation,
        ICurrentUser currentUser,
        Guid ticketId) => operation switch
        {
            Operation.StartWork => RunWithLoggerAsync<StartWorkHandler>(logger =>
                new StartWorkHandler(currentUser, _tickets, _unitOfWork, _time, logger)
                    .HandleAsync(new StartWorkCommand(ticketId), CancellationToken)),
            Operation.ResolveTicket => RunWithLoggerAsync<ResolveTicketHandler>(logger =>
                new ResolveTicketHandler(currentUser, _tickets, _unitOfWork, _time, logger)
                    .HandleAsync(new ResolveTicketCommand(ticketId), CancellationToken)),
            Operation.ReopenTicket => RunWithLoggerAsync<ReopenTicketHandler>(logger =>
                new ReopenTicketHandler(currentUser, _tickets, _unitOfWork, _time, logger)
                    .HandleAsync(new ReopenTicketCommand(ticketId), CancellationToken)),
            Operation.CloseTicket => RunWithLoggerAsync<CloseTicketHandler>(logger =>
                new CloseTicketHandler(currentUser, _tickets, _unitOfWork, _time, logger)
                    .HandleAsync(new CloseTicketCommand(ticketId), CancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
        };

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
