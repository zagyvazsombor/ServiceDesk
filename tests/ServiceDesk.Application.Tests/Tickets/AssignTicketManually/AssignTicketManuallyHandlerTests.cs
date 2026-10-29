using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.AssignTicketManually;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.AssignTicketManually;

public sealed class AssignTicketManuallyHandlerTests
{
    private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
    private readonly IAgentRepository _agents = Substitute.For<IAgentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly RecordingLogger<AssignTicketManuallyHandler> _logger = new();
    private readonly Category _category = TestData.CreateCategory();

    public AssignTicketManuallyHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous())
            .HandleAsync(new AssignTicketManuallyCommand(Guid.CreateVersion7(), Guid.CreateVersion7()), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading()
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _category));
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));

        // Act
        var result = await CreateHandler(TestUsers.Reporter())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, agent.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        ticket.AssigneeAgentId.ShouldBeNull();
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_EmptyAgentId_ReturnsValidationErrorWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(Guid.CreateVersion7(), Guid.Empty), CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.AgentId");
        result.Error.Message.ShouldBe("Válassz ügyintézőt.");
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_UnknownTicket_ReturnsTicketNotFound()
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _category));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(Guid.CreateVersion7(), agent.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_UnknownAgent_ReturnsAgentNotFound()
    {
        // Arrange
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, Guid.CreateVersion7()), CancellationToken);

        // Assert
        result.Error.ShouldBe(AgentErrors.NotFound);
        ticket.AssigneeAgentId.ShouldBeNull();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    [Trait("BR", "BR-10")]
    public async Task HandleAsync_NewTicketAndEligibleAgent_AssignsAsAdminSavesAndLogs()
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _category));
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));
        _time.Advance(TimeSpan.FromMinutes(5));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, agent.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.Assigned, agent.Id));
        var assignment = ticket.History.Single(entry => entry.Action == TicketHistoryAction.Assigned);
        (assignment.ActorUserId, assignment.At).ShouldBe((TestUsers.UserId, _time.GetUtcNow()));
        agent.LastAssignedAt.ShouldBe(_time.GetUtcNow());
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Properties["AssigneeAgentId"].ShouldBe(agent.Id);
    }

    [Theory]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Resolved)]
    [Trait("BR", "BR-04")]
    public async Task HandleAsync_AssignedTicketToOtherAgent_ReassignsWithoutStatusChange(TicketStatus status)
    {
        // Arrange
        var anna = TestData.CreateAgent("Kiss Anna", _category);
        var bela = AgentCanBeLoaded(TestData.CreateAgent("Nagy Béla", _category));
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(status, _category, anna));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, bela.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((status, bela.Id));
        var entry = ticket.History.Last();
        (entry.Action, entry.ActorUserId).ShouldBe((TicketHistoryAction.Reassigned, TestUsers.UserId));
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public async Task HandleAsync_TicketOfInactiveAgentToActiveAgent_Reassigns()
    {
        // Arrange
        var anna = TestData.CreateAgent("Kiss Anna", _category);
        var bela = AgentCanBeLoaded(TestData.CreateAgent("Nagy Béla", _category));
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.InProgress, _category, anna));
        anna.Deactivate();

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, bela.Id), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.InProgress, bela.Id));
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task HandleAsync_ClosedTicket_ReturnsClosedCannotBeAssignedWithoutSave()
    {
        // Arrange
        var anna = TestData.CreateAgent("Kiss Anna", _category);
        var bela = AgentCanBeLoaded(TestData.CreateAgent("Nagy Béla", _category));
        var ticket = TicketCanBeLoaded(TestData.CreateTicketInStatus(TicketStatus.Closed, _category, anna));
        var historyCount = ticket.History.Count;

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, bela.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(TicketErrors.ClosedCannotBeAssigned);
        result.Error.Message.ShouldBe("Lezárt jegy nem osztható ki és nem osztható át.");
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.Closed, anna.Id));
        ticket.History.Count.ShouldBe(historyCount);
        bela.LastAssignedAt.ShouldBeNull();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-04")]
    [Trait("BR", "BR-12")]
    public async Task HandleAsync_InactiveAgent_ReturnsAgentInactiveWithoutSave()
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _category));
        agent.Deactivate();
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, agent.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(AgentErrors.Inactive);
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.New, null));
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task HandleAsync_AgentWithoutCompetency_ReturnsMissingCompetencyWithoutSave()
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", TestData.CreateCategory("NETWORK")));
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, agent.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(AgentErrors.MissingCompetency);
        ticket.AssigneeAgentId.ShouldBeNull();
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog()
    {
        // Arrange
        var agent = AgentCanBeLoaded(TestData.CreateAgent("Kiss Anna", _category));
        var ticket = TicketCanBeLoaded(TestData.CreateTicket(_category));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new AssignTicketManuallyCommand(ticket.Id, agent.Id), CancellationToken);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        _logger.Entries.ShouldBeEmpty();
    }

    private AssignTicketManuallyHandler CreateHandler(ICurrentUser currentUser) =>
        new(currentUser, new AssignTicketManuallyValidator(), _tickets, _agents, _unitOfWork, _time, _logger);

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
        await _agents.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
