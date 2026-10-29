using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets;
using ServiceDesk.Application.Tickets.CreateTicket;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.CreateTicket;

// The real validator, TicketAssignmentService and LeastLoadedAssignmentStrategy run; only the ports are substituted.
public sealed class CreateTicketHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly ITicketRepository _tickets = Substitute.For<ITicketRepository>();
    private readonly IAgentRepository _agents = Substitute.For<IAgentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly RecordingLogger<CreateTicketHandler> _logger = new();
    private readonly Category _category = TestData.CreateCategory();
    private Ticket? _addedTicket;

    public CreateTicketHandlerTests()
    {
        _categories.GetByIdAsync(_category.Id, Arg.Any<CancellationToken>()).Returns(_category);
        _agents.GetEligibleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AgentCandidate>());
        _tickets.Add(Arg.Do<Ticket>(ticket => _addedTicket = ticket));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("BR", "BR-01")]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutChanges()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public async Task HandleAsync_Admin_ReturnsForbiddenWithoutChanges()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public async Task HandleAsync_SignedInUserWithoutRole_ReturnsForbidden()
    {
        // Act
        var result = await CreateHandler(TestUsers.SignedIn()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public async Task HandleAsync_SeveralInvalidFields_ReturnsEveryFieldErrorWithoutChanges()
    {
        // Arrange
        var command = new CreateTicketCommand("Hiba", string.Empty, Impact: null, Urgency.Low, Guid.Empty);

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.Code.ShouldBe(TicketErrors.TitleLength.Code);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Keys.ShouldBe(
            [nameof(CreateTicketCommand.Title), nameof(CreateTicketCommand.Description), nameof(CreateTicketCommand.Impact), nameof(CreateTicketCommand.CategoryId)],
            ignoreOrder: true);
        result.Error.FieldErrors[nameof(CreateTicketCommand.Description)].ShouldBe([TicketErrors.DescriptionLength.Message]);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_UnknownCategory_ReturnsCategoryNotFound()
    {
        // Arrange
        var command = ValidCommand() with { CategoryId = Guid.CreateVersion7() };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.ShouldBe(CategoryErrors.NotFound);
        _tickets.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_InactiveCategory_ReturnsCategoryInactive()
    {
        // Arrange
        var inactiveCategory = TestData.CreateInactiveCategory();
        _categories.GetByIdAsync(inactiveCategory.Id, Arg.Any<CancellationToken>()).Returns(inactiveCategory);
        var command = ValidCommand() with { CategoryId = inactiveCategory.Id };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.ShouldBe(CategoryErrors.Inactive);
        _tickets.DidNotReceiveWithAnyArgs().Add(default!);
        await _agents.DidNotReceiveWithAnyArgs().GetEligibleCandidatesAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    [Trait("BR", "BR-05")]
    [Trait("BR", "BR-11")]
    public async Task HandleAsync_EligibleAgent_SavesAssignedTicketOnceAndReturnsItsId()
    {
        // Arrange
        var agent = TestData.CreateAgent("Kiss Anna", _category);
        _agents.GetEligibleCandidatesAsync(_category.Id, Arg.Any<CancellationToken>()).Returns([TestData.CandidateOf(agent)]);
        _agents.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var ticket = _addedTicket.ShouldNotBeNull();
        result.Value.ShouldBe(ticket.Id);
        ticket.ReporterId.ShouldBe(TestUsers.UserId);
        ticket.Title.ShouldBe(TestData.ValidTitle);
        ticket.CategoryId.ShouldBe(_category.Id);
        ticket.Priority.ShouldBe(Priority.High); // High × Medium
        ticket.CreatedAt.ShouldBe(TestData.Now);
        ticket.Status.ShouldBe(TicketStatus.Assigned);
        ticket.AssigneeAgentId.ShouldBe(agent.Id);
        agent.LastAssignedAt.ShouldBe(TestData.Now);
        Received.InOrder(() =>
        {
            _tickets.Add(ticket);
            _unitOfWork.SaveChangesAsync(CancellationToken);
        });
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task HandleAsync_NoEligibleAgent_SavesNewTicketWithoutAssignee()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var ticket = _addedTicket.ShouldNotBeNull();
        ticket.Status.ShouldBe(TicketStatus.New);
        ticket.AssigneeAgentId.ShouldBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_Success_LogsTicketCreatedWithoutContent()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Properties["TicketId"].ShouldBe(result.Value);
        entry.Properties["ReporterId"].ShouldBe(TestUsers.UserId);
        entry.Properties["AssigneeAgentId"].ShouldBeNull();
        entry.Properties.Keys.ShouldBe(["TicketId", "ReporterId", "AssigneeAgentId", "{OriginalFormat}"], ignoreOrder: true);
    }

    [Fact]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        _logger.Entries.ShouldBeEmpty();
    }

    private CreateTicketCommand ValidCommand() =>
        new(TestData.ValidTitle, TestData.ValidDescription, Impact.High, Urgency.Medium, _category.Id);

    private CreateTicketHandler CreateHandler(ICurrentUser currentUser) => new(
        currentUser,
        new CreateTicketValidator(),
        _categories,
        _tickets,
        new TicketAssignmentService(_agents, new LeastLoadedAssignmentStrategy(), _time),
        _unitOfWork,
        _time,
        _logger);

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _categories.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        _tickets.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
