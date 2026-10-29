using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets;
using ServiceDesk.Application.Tickets.RetryAutoAssign;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.RetryAutoAssign;

// The real TicketAssignmentService and LeastLoadedAssignmentStrategy run; only the ports are substituted.
public sealed class RetryAutoAssignHandlerTests
{
    private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
    private readonly IAgentRepository _agents = Substitute.For<IAgentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly RecordingLogger<RetryAutoAssignHandler> _logger = new();
    private readonly Category _category = TestData.CreateCategory();

    public RetryAutoAssignHandlerTests()
    {
        _agents.GetEligibleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AgentCandidate>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous())
            .HandleAsync(new RetryAutoAssignCommand(Guid.CreateVersion7()), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new RetryAutoAssignCommand(ticket.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        ticket.Status.ShouldBe(TicketStatus.New);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_UnknownTicket_ReturnsNotFound()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new RetryAutoAssignCommand(Guid.CreateVersion7()), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    [Trait("BR", "BR-06")]
    public async Task HandleAsync_NewTicketWithCandidate_AssignsAsSystemSavesAndReturnsAgentId()
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _category));
        _agents.GetEligibleCandidatesAsync(_category.Id, Arg.Any<CancellationToken>()).Returns([TestData.CandidateOf(agent)]);
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new RetryAutoAssignCommand(ticket.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(agent.Id);
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.Assigned, agent.Id));
        ticket.History.Single(entry => entry.Action == TicketHistoryAction.Assigned).ActorUserId.ShouldBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Properties["AssigneeAgentId"].ShouldBe(agent.Id);
        entry.Properties["ActorUserId"].ShouldBe(TestUsers.UserId);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task HandleAsync_NewTicketWithoutCandidate_ReturnsNullWithoutSave()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new RetryAutoAssignCommand(ticket.Id), CancellationToken);

        // Assert: not an error, the ticket simply stays New without an assignee.
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.New, null));
        ticket.History.ShouldHaveSingleItem();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
        _logger.Entries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [Trait("BR", "BR-06")]
    public async Task HandleAsync_TicketNotNew_ReturnsAutoAssignmentNotAllowed(TicketStatus status)
    {
        // Arrange
        var anna = TestData.CreateAgent("Kiss Anna", _category);
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(status, _category, anna));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new RetryAutoAssignCommand(ticket.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.AutoAssignmentNotAllowed);
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((status, anna.Id));
        await _agents.DidNotReceiveWithAnyArgs().GetEligibleCandidatesAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task HandleAsync_ClosedTicket_ReturnsAutoAssignmentNotAllowed()
    {
        // Arrange
        var anna = TestData.CreateAgent("Kiss Anna", _category);
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.Closed, _category, anna));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new RetryAutoAssignCommand(ticket.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.AutoAssignmentNotAllowed);
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        ticket.Status.ShouldBe(TicketStatus.Closed);
        await _agents.DidNotReceiveWithAnyArgs().GetEligibleCandidatesAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog()
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _category));
        _agents.GetEligibleCandidatesAsync(_category.Id, Arg.Any<CancellationToken>()).Returns([TestData.CandidateOf(agent)]);
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new RetryAutoAssignCommand(ticket.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        _logger.Entries.ShouldBeEmpty();
    }

    private RetryAutoAssignHandler CreateHandler(ICurrentUser currentUser) => new(
        currentUser,
        _tickets,
        new TicketAssignmentService(_agents, new LeastLoadedAssignmentStrategy(), _time),
        _unitOfWork,
        _logger);

    private Ticket TicketCanBeLoaded(Ticket ticket)
    {
        _tickets.GetByIdAsync(ticket.Id, Arg.Any<CancellationToken>()).Returns(ticket);
        return ticket;
    }

    private Agent AgentCanBeLoaded(Agent agent)
    {
        _agents.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);
        return agent;
    }

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _tickets.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
