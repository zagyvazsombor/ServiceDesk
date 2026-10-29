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
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.CreateTicket;

// The real validator, TicketAssignmentService and LeastLoadedAssignmentStrategy run; only the ports are substituted.
public sealed class CreateTicketHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly ICategorySuggestionRepository _suggestions = Substitute.For<ICategorySuggestionRepository>();
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
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_SuggestionOfChosenCategory_DecidesAcceptedAndSavesOnce()
    {
        // Arrange
        var suggestion = SuggestionOf(TestUsers.UserId, _category);
        var command = ValidCommand() with { SuggestionId = suggestion.Id };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        suggestion.Decision.ShouldBe(CategorySuggestionDecision.Accepted);
        suggestion.TicketId.ShouldBe(result.Value);
        suggestion.DecidedAt.ShouldBe(TestData.Now);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_SuggestionOfOtherCategory_DecidesOverriddenAndKeepsChosenCategory()
    {
        // Arrange
        var suggestion = SuggestionOf(TestUsers.UserId, TestData.CreateCategory("NETWORK"));
        var command = ValidCommand() with { SuggestionId = suggestion.Id };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _addedTicket.ShouldNotBeNull().CategoryId.ShouldBe(_category.Id);
        suggestion.Decision.ShouldBe(CategorySuggestionDecision.Overridden);
        suggestion.TicketId.ShouldBe(result.Value);
        suggestion.DecidedAt.ShouldBe(TestData.Now);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_SuggestionOfOtherUser_ReturnsNotFoundWithoutChangesAndLogsWarning()
    {
        // Arrange
        var suggestion = SuggestionOf("other-reporter", _category);
        var command = ValidCommand() with { SuggestionId = suggestion.Id };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.ShouldBe(CategorySuggestionErrors.NotFound);
        suggestion.Decision.ShouldBe(CategorySuggestionDecision.Pending);
        suggestion.TicketId.ShouldBeNull();
        await ShouldNotHaveCreatedTicketAsync();
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["UserId"].ShouldBe(TestUsers.UserId);
        entry.Properties["SuggestionId"].ShouldBe(suggestion.Id);
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_UnknownSuggestion_ReturnsNotFoundWithoutChanges()
    {
        // Arrange
        var command = ValidCommand() with { SuggestionId = Guid.CreateVersion7() };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.ShouldBe(CategorySuggestionErrors.NotFound);
        await ShouldNotHaveCreatedTicketAsync();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_AlreadyDecidedSuggestion_ReturnsAlreadyDecidedWithoutChanges()
    {
        // Arrange
        var suggestion = SuggestionOf(TestUsers.UserId, _category);
        var firstTicketId = Guid.CreateVersion7();
        suggestion.Decide(firstTicketId, CategorySuggestionDecision.Accepted, TestData.Now.AddMinutes(-1)).IsSuccess.ShouldBeTrue();
        var command = ValidCommand() with { SuggestionId = suggestion.Id };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.ShouldBe(CategorySuggestionErrors.AlreadyDecided);
        suggestion.TicketId.ShouldBe(firstTicketId);
        await ShouldNotHaveCreatedTicketAsync();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_EmptySuggestionId_ReturnsValidationErrorWithoutLoadingSuggestion()
    {
        // Arrange
        var command = ValidCommand() with { SuggestionId = Guid.Empty };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.Code.ShouldBe($"Validation.{nameof(CreateTicketCommand.SuggestionId)}");
        await _suggestions.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await ShouldNotHaveCreatedTicketAsync();
    }

    [Fact]
    public async Task HandleAsync_WithoutSuggestion_DoesNotLoadSuggestion()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _suggestions.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_SuggestionDecided_LogsDecisionWithoutReasoning()
    {
        // Arrange
        var suggestion = SuggestionOf(TestUsers.UserId, _category);
        var command = ValidCommand() with { SuggestionId = suggestion.Id };

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        _logger.Entries.Count.ShouldBe(2);
        var entry = _logger.Entries[1];
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Properties["SuggestionId"].ShouldBe(suggestion.Id);
        entry.Properties["TicketId"].ShouldBe(result.Value);
        entry.Properties["Decision"].ShouldBe(CategorySuggestionDecision.Accepted);
        entry.Properties.Keys.ShouldBe(["SuggestionId", "TicketId", "Decision", "{OriginalFormat}"], ignoreOrder: true);
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
        _suggestions,
        _tickets,
        new TicketAssignmentService(_agents, new LeastLoadedAssignmentStrategy(), _time),
        _unitOfWork,
        _time,
        _logger);

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _categories.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _suggestions.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        _tickets.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    // A Pending suggestion of the given user, as SuggestCategoryHandler saved it; the repository returns it.
    private CategorySuggestion SuggestionOf(string requestedByUserId, Category suggestedCategory)
    {
        var suggestion = CategorySuggestion.Create(
            requestedByUserId,
            suggestedCategory,
            0.82,
            "A leírás szerint a laptop nem kapcsol be.",
            "gemma3:4b",
            "v1",
            TestData.Now.AddMinutes(-2)).Value;
        _suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);
        return suggestion;
    }

    private async Task ShouldNotHaveCreatedTicketAsync()
    {
        _tickets.DidNotReceiveWithAnyArgs().Add(default!);
        await _agents.DidNotReceiveWithAnyArgs().GetEligibleCandidatesAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
