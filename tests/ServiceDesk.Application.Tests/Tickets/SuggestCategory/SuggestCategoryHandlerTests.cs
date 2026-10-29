using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using ServiceDesk.Application.Common.Ai;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.SuggestCategory;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.SuggestCategory;

// The real validator runs; the rate limiter, the suggester and the repositories are substituted.
public sealed class SuggestCategoryHandlerTests
{
    private const string Model = "gemma3:4b";
    private const string Reasoning = "A leírás vezeték nélküli kapcsolódási hibát említ.";

    private readonly ISuggestionRateLimiter _rateLimiter = Substitute.For<ISuggestionRateLimiter>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly ICategorySuggester _suggester = Substitute.For<ICategorySuggester>();
    private readonly ICategorySuggestionRepository _suggestions = Substitute.For<ICategorySuggestionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(TestData.Now);
    private readonly RecordingLogger<SuggestCategoryHandler> _logger = new();
    private readonly Category _hardware = TestData.CreateCategory("HARDWARE");
    private readonly Category _network = Category.Create("NETWORK", "Hálózat", "Hálózati kapcsolat, Wi-Fi és VPN hibái.").Value;
    private CategorySuggestionInput? _sentInput;
    private CategorySuggestion? _addedSuggestion;

    public SuggestCategoryHandlerTests()
    {
        _rateLimiter.TryAcquire(Arg.Any<string>()).Returns(true);
        _categories.GetActiveAsync(Arg.Any<CancellationToken>()).Returns([_hardware, _network]);
        _suggester.SuggestAsync(Arg.Do<CategorySuggestionInput>(input => _sentInput = input), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CategorySuggestionOutput("NETWORK", 0.8, Reasoning, Model, "v1")));
        _suggestions.Add(Arg.Do<CategorySuggestion>(suggestion => _addedSuggestion = suggestion));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutCallingTheModel()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveRequestedSuggestionAsync();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_Admin_ReturnsForbiddenWithoutCallingTheModel()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveRequestedSuggestionAsync();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_SignedInUserWithoutRole_ReturnsForbidden()
    {
        // Act
        var result = await CreateHandler(TestUsers.SignedIn()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveRequestedSuggestionAsync();
    }

    [Fact]
    public async Task HandleAsync_InvalidTitleAndDescription_ReturnsBothFieldErrorsWithoutUsingTheRateLimit()
    {
        // Arrange
        var command = new SuggestCategoryCommand("Hiba", "Rövid");

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Keys.ShouldBe(
            [nameof(SuggestCategoryCommand.Title), nameof(SuggestCategoryCommand.Description)],
            ignoreOrder: true);
        await ShouldNotHaveRequestedSuggestionAsync();
    }

    [Fact]
    public async Task HandleAsync_RateLimitExceeded_ReturnsRateLimitedWithWarningAndWithoutCallingTheModel()
    {
        // Arrange
        _rateLimiter.TryAcquire(TestUsers.UserId).Returns(false);

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.RateLimited);
        result.Error.Type.ShouldBe(ErrorType.Unavailable);
        await _categories.DidNotReceiveWithAnyArgs().GetActiveAsync(CancellationToken);
        await _suggester.DidNotReceiveWithAnyArgs().SuggestAsync(default!, CancellationToken);
        await ShouldNotHaveSavedAsync();
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["UserId"].ShouldBe(TestUsers.UserId);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task HandleAsync_NoActiveCategory_ReturnsNoSuggestionWithoutCallingTheModel()
    {
        // Arrange
        _categories.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Category>());

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.NoSuggestion);
        await _suggester.DidNotReceiveWithAnyArgs().SuggestAsync(default!, CancellationToken);
        await ShouldNotHaveSavedAsync();
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task HandleAsync_SuggesterHasNoSuggestion_ReturnsNoSuggestionWithoutSaving()
    {
        // Arrange
        _suggester.SuggestAsync(Arg.Any<CategorySuggestionInput>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CategorySuggestionOutput>(AiErrors.NoSuggestion));

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.NoSuggestion);
        await ShouldNotHaveSavedAsync();
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task HandleAsync_SuggestedCodeIsNotAnActiveCategory_ReturnsNoSuggestionWithoutSaving()
    {
        // Arrange
        _suggester.SuggestAsync(Arg.Any<CategorySuggestionInput>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CategorySuggestionOutput("OTHER", 0.9, Reasoning, Model, "v1")));

        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.NoSuggestion);
        await ShouldNotHaveSavedAsync();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task HandleAsync_ValidSuggestion_SavesPendingSuggestionOnceAndReturnsIt()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var suggestion = _addedSuggestion.ShouldNotBeNull();
        suggestion.Decision.ShouldBe(CategorySuggestionDecision.Pending);
        suggestion.RequestedByUserId.ShouldBe(TestUsers.UserId);
        suggestion.SuggestedCategoryId.ShouldBe(_network.Id);
        suggestion.Confidence.ShouldBe(0.8);
        suggestion.Reasoning.ShouldBe(Reasoning);
        suggestion.Model.ShouldBe(Model);
        suggestion.PromptVersion.ShouldBe("v1");
        suggestion.CreatedAt.ShouldBe(TestData.Now);
        suggestion.TicketId.ShouldBeNull();
        suggestion.DecidedAt.ShouldBeNull();
        result.Value.ShouldBe(new CategorySuggestionDto(suggestion.Id, _network.Id, "Hálózat", 0.8, Reasoning));
        _rateLimiter.Received(1).TryAcquire(TestUsers.UserId);
        Received.InOrder(() =>
        {
            _suggestions.Add(suggestion);
            _unitOfWork.SaveChangesAsync(CancellationToken);
        });
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task HandleAsync_ValidCommand_SendsOnlyTheTrimmedTextAndTheActiveCategoriesToTheModel()
    {
        // Arrange
        var command = new SuggestCategoryCommand("  Nem megy a wifi  ", "  Reggel óta nem csatlakozik a laptop a hálózathoz.  ");

        // Act
        await CreateHandler(TestUsers.Reporter()).HandleAsync(command, CancellationToken);

        // Assert
        var input = _sentInput.ShouldNotBeNull();
        input.Title.ShouldBe("Nem megy a wifi");
        input.Description.ShouldBe("Reggel óta nem csatlakozik a laptop a hálózathoz.");
        input.Categories.ShouldBe(
        [
            new SuggestionCategory(_hardware.Code, _hardware.Name, _hardware.Description),
            new SuggestionCategory(_network.Code, _network.Name, _network.Description),
        ]);
        // AGENTS.md §9: no user Id reaches the adapter, so it cannot reach the model either.
        input.ToString().ShouldNotContain(TestUsers.UserId);
    }

    [Fact]
    public async Task HandleAsync_ValidSuggestion_LogsBusinessEventWithoutText()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Properties["SuggestionId"].ShouldBe(result.Value.SuggestionId);
        entry.Properties["UserId"].ShouldBe(TestUsers.UserId);
        entry.Properties["CategoryCode"].ShouldBe("NETWORK");
        entry.Properties["Confidence"].ShouldBe(0.8);
        entry.Properties.Keys.ShouldBe(["SuggestionId", "UserId", "CategoryCode", "Confidence", "{OriginalFormat}"], ignoreOrder: true);
    }

    private static SuggestCategoryCommand ValidCommand() => new(TestData.ValidTitle, TestData.ValidDescription);

    private SuggestCategoryHandler CreateHandler(ICurrentUser currentUser) => new(
        currentUser,
        new SuggestCategoryValidator(),
        _rateLimiter,
        _categories,
        _suggester,
        _suggestions,
        _unitOfWork,
        _time,
        _logger);

    private async Task ShouldNotHaveRequestedSuggestionAsync()
    {
        _rateLimiter.DidNotReceiveWithAnyArgs().TryAcquire(default!);
        await _categories.DidNotReceiveWithAnyArgs().GetActiveAsync(CancellationToken);
        await _suggester.DidNotReceiveWithAnyArgs().SuggestAsync(default!, CancellationToken);
        await ShouldNotHaveSavedAsync();
    }

    private async Task ShouldNotHaveSavedAsync()
    {
        _suggestions.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
