using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets;
using ServiceDesk.Application.Tickets.RecategorizeTicket;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.RecategorizeTicket;

// The real TicketAssignmentService and LeastLoadedAssignmentStrategy run; only the ports are substituted. Every ticket
// starts in HARDWARE and is moved to NETWORK.
public sealed class RecategorizeTicketHandlerTests
{
    private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IAgentRepository _agents = Substitute.For<IAgentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly RecordingLogger<RecategorizeTicketHandler> _logger = new();
    private readonly Category _hardware = TestData.CreateCategory("HARDWARE");
    private readonly Category _network = TestData.CreateCategory("NETWORK");

    public RecategorizeTicketHandlerTests()
    {
        _categories.GetByIdAsync(_hardware.Id, Arg.Any<CancellationToken>()).Returns(_hardware);
        _categories.GetByIdAsync(_network.Id, Arg.Any<CancellationToken>()).Returns(_network);
        _agents.GetEligibleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AgentCandidate>());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous())
            .HandleAsync(new RecategorizeTicketCommand(Guid.CreateVersion7(), _network.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_hardware));

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        ticket.CategoryId.ShouldBe(_hardware.Id);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_EmptyCategoryId_ReturnsValidationErrorWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new RecategorizeTicketCommand(Guid.CreateVersion7(), Guid.Empty), CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.CategoryId");
        result.Error.Type.ShouldBe(ErrorType.Validation);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_UnknownTicket_ReturnsTicketNotFound()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new RecategorizeTicketCommand(Guid.CreateVersion7(), _network.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_UnknownCategory_ReturnsCategoryNotFound()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_hardware));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new RecategorizeTicketCommand(ticket.Id, Guid.CreateVersion7()), CancellationToken);

        // Assert
        result.Error.ShouldBe(CategoryErrors.NotFound);
        ticket.CategoryId.ShouldBe(_hardware.Id);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task HandleAsync_ClosedTicket_ReturnsClosedCannotBeModifiedWithoutAssignment()
    {
        // Arrange
        var anna = TestData.CreateAgent("Kiss Anna", _hardware);
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.Closed, _hardware, anna));
        var historyCount = ticket.History.Count;

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.ClosedCannotBeModified);
        (ticket.CategoryId, ticket.Status, ticket.AssigneeAgentId).ShouldBe((_hardware.Id, TicketStatus.Closed, anna.Id));
        ticket.History.Count.ShouldBe(historyCount);
        await _agents.DidNotReceiveWithAnyArgs().GetEligibleCandidatesAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task HandleAsync_ResolvedTicket_ReturnsRecategorizationNotAllowed()
    {
        // Arrange
        var anna = TestData.CreateAgent("Kiss Anna", _hardware);
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.Resolved, _hardware, anna));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.RecategorizationNotAllowed);
        ticket.CategoryId.ShouldBe(_hardware.Id);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_InactiveCategory_ReturnsCategoryInactive()
    {
        // Arrange
        var inactive = TestData.CreateInactiveCategory();
        _categories.GetByIdAsync(inactive.Id, Arg.Any<CancellationToken>()).Returns(inactive);
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_hardware));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new RecategorizeTicketCommand(ticket.Id, inactive.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(CategoryErrors.Inactive);
        ticket.CategoryId.ShouldBe(_hardware.Id);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Theory]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [Trait("BR", "BR-07")]
    public async Task HandleAsync_AssigneeEligibleForNewCategory_KeepsAssignment(TicketStatus status)
    {
        // Arrange
        var anna = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _hardware, _network));
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(status, _hardware, anna));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.CategoryId, ticket.Status, ticket.AssigneeAgentId).ShouldBe((_network.Id, status, anna.Id));
        var entry = ticket.History.Last();
        (entry.Action, entry.ActorUserId).ShouldBe((TicketHistoryAction.Recategorized, TestUsers.UserId));
        await _agents.DidNotReceiveWithAnyArgs().GetEligibleCandidatesAsync(default, CancellationToken);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Theory]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [Trait("BR", "BR-05")]
    [Trait("BR", "BR-07")]
    public async Task HandleAsync_AssigneeNotEligibleAndCandidateExists_ReassignsKeepingStatus(TicketStatus status)
    {
        // Arrange
        var anna = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _hardware));
        var bela = AgentCanBeLoaded(TestData.CreateAgent("Nagy Béla", _network));
        CandidatesForNetworkAre(TestData.CandidateOf(bela));
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(status, _hardware, anna));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.CategoryId, ticket.Status, ticket.AssigneeAgentId).ShouldBe((_network.Id, status, bela.Id));
        var entry = ticket.History.Last();
        (entry.Action, entry.ActorUserId, entry.OldValue, entry.NewValue)
            .ShouldBe((TicketHistoryAction.Reassigned, null, anna.Id.ToString(), bela.Id.ToString()));
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        _logger.Entries.ShouldHaveSingleItem().Properties["AssigneeAgentId"].ShouldBe(bela.Id);
    }

    [Theory]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [Trait("BR", "BR-07")]
    public async Task HandleAsync_AssigneeNotEligibleAndNoCandidate_UnassignsTicketToNew(TicketStatus status)
    {
        // Arrange
        var anna = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _hardware));
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(status, _hardware, anna));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.CategoryId, ticket.Status, ticket.AssigneeAgentId).ShouldBe((_network.Id, TicketStatus.New, null));
        ticket.History.TakeLast(3).Select(entry => (entry.Action, entry.ActorUserId)).ShouldBe(
        [
            (TicketHistoryAction.Recategorized, TestUsers.UserId),
            (TicketHistoryAction.Unassigned, null),
            (TicketHistoryAction.StatusChanged, null),
        ]);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    [Trait("BR", "BR-07")]
    [Trait("BR", "BR-12")]
    public async Task HandleAsync_InactiveAssigneeWithCompetency_ReassignsToEligibleAgent()
    {
        // Arrange
        var anna = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _hardware, _network));
        var bela = AgentCanBeLoaded(TestData.CreateAgent("Nagy Béla", _network));
        CandidatesForNetworkAre(TestData.CandidateOf(bela));
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.InProgress, _hardware, anna));
        anna.Deactivate();

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.InProgress, bela.Id));
    }

    [Fact]
    [Trait("BR", "BR-05")]
    public async Task HandleAsync_NewTicketWithCandidate_AssignsTicketAutomatically()
    {
        // Arrange
        var bela = AgentCanBeLoaded(TestData.CreateAgent("Nagy Béla", _network));
        CandidatesForNetworkAre(TestData.CandidateOf(bela));
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_hardware));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.CategoryId, ticket.Status, ticket.AssigneeAgentId).ShouldBe((_network.Id, TicketStatus.Assigned, bela.Id));
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task HandleAsync_NewTicketWithoutCandidate_SavesNewCategoryAndStaysNew()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_hardware));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.CategoryId, ticket.Status, ticket.AssigneeAgentId).ShouldBe((_network.Id, TicketStatus.New, null));
        ticket.History.Last().Action.ShouldBe(TicketHistoryAction.Recategorized);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_SameCategory_ReturnsSuccessWithoutAssignmentOrSave()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_hardware));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new RecategorizeTicketCommand(ticket.Id, _hardware.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ticket.History.ShouldHaveSingleItem();
        await _agents.DidNotReceiveWithAnyArgs().GetEligibleCandidatesAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_hardware));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(ticket), CancellationToken);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        _logger.Entries.ShouldBeEmpty();
    }

    private RecategorizeTicketCommand Command(Ticket ticket) => new(ticket.Id, _network.Id);

    private RecategorizeTicketHandler CreateHandler(ICurrentUser currentUser) => new(
        currentUser,
        new RecategorizeTicketValidator(),
        _tickets,
        _categories,
        _agents,
        new TicketAssignmentService(_agents, new LeastLoadedAssignmentStrategy(), _time),
        _unitOfWork,
        _time,
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

    private void CandidatesForNetworkAre(params AgentCandidate[] candidates) =>
        _agents.GetEligibleCandidatesAsync(_network.Id, Arg.Any<CancellationToken>()).Returns(candidates);

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _tickets.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _categories.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
