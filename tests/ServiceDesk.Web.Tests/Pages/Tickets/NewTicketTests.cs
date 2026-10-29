using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets;
using ServiceDesk.Application.Tickets.CreateTicket;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.SuggestCategory;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Web.Components.Pages.Tickets;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages.Tickets;

public sealed class NewTicketTests : BunitContext
{
    private const string SubmitText = "Jegy rögzítése";
    private const string Title = "Nem indul a laptop";
    private const string Description = "Bekapcsoláskor a laptop képernyője fekete marad.";

    private static readonly DateTimeOffset SuggestedAt = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

    private readonly Category _hardware = Category.Create("HARDWARE", "Hardver", "Számítógépek, perifériák és egyéb eszközök.").Value;
    private readonly Category _network = Category.Create("NETWORK", "Hálózat", "Vezetékes és vezeték nélküli hálózat.").Value;
    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();

    public NewTicketTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _runner.RunAsync(
                Arg.Any<Func<GetActiveCategoriesHandler, CancellationToken, Task<Result<IReadOnlyList<ActiveCategoryDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ActiveCategoryDto>>([DtoOf(_hardware), DtoOf(_network)]));
    }

    [Fact]
    public async Task Submit_EmptyFields_DoesNotRunCreateTicketAndShowsRequiredErrors()
    {
        // Arrange
        var cut = Render<NewTicket>();

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<CreateTicketHandler, Result<Guid>>(
            default!, Xunit.TestContext.Current.CancellationToken);
        cut.Markup.ShouldContain("Add meg a jegy címét.");
        cut.Markup.ShouldContain("Írd le a problémát.");
        cut.Markup.ShouldContain("Válaszd ki a hatást.");
        cut.Markup.ShouldContain("Válaszd ki a sürgősséget.");
        cut.Markup.ShouldContain("Válassz kategóriát.");
    }

    [Fact]
    public async Task Submit_SuccessfulResult_ShowsSnackbarAndNavigatesToTicketDetails()
    {
        // Arrange
        var ticketId = Guid.CreateVersion7();
        CreateTicketReturns(ticketId);
        var cut = Render<NewTicket>();
        await FillFormAsync(cut);

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        _snackbar.Received(1).Add("A jegy rögzítve.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe($"http://localhost/tickets/{ticketId}");
    }

    [Fact]
    public async Task Submit_FailedResult_ShowsSnackbarMessageAndStaysOnPage()
    {
        // Arrange
        CreateTicketReturns(CategoryErrors.Inactive);
        var cut = Render<NewTicket>();
        await FillFormAsync(cut);

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        _snackbar.Received(1).Add(CategoryErrors.Inactive.Message, Severity.Error, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe("http://localhost/");
    }

    [Fact]
    public async Task Submit_ValidationResult_ShowsErrorsAtFieldsWithoutSnackbar()
    {
        // Arrange
        CreateTicketReturns(Error.Validation(
        [
            (nameof(CreateTicketCommand.Title), TicketErrors.TitleLength.Message),
            (nameof(CreateTicketCommand.Description), TicketErrors.DescriptionLength.Message),
        ]));
        var cut = Render<NewTicket>();
        await FillFormAsync(cut);

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        TextField(cut, "Cím").Markup.ShouldContain(TicketErrors.TitleLength.Message);
        TextField(cut, "Leírás").Markup.ShouldContain(TicketErrors.DescriptionLength.Message);
        _snackbar.DidNotReceiveWithAnyArgs().Add(default(string)!, default, default, default);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task SelectImpactAndUrgency_HighAndHigh_ShowsCriticalExpectedPriority()
    {
        // Arrange
        var cut = Render<NewTicket>();
        cut.FindAll("#expected-priority").ShouldBeEmpty();

        // Act
        await cut.InvokeAsync(() => Select<Impact?>(cut, "Hatás").Instance.SelectOption(Impact.High));
        await cut.InvokeAsync(() => Select<Urgency?>(cut, "Sürgősség").Instance.SelectOption(Urgency.High));

        // Assert
        cut.Find("#expected-priority").TextContent.ShouldContain("Várható prioritás: Kritikus");
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public void SuggestionButton_TitleAndDescriptionTooShort_DisabledUntilBothValid()
    {
        // Arrange
        var cut = Render<NewTicket>();
        SuggestionButton(cut).HasAttribute("disabled").ShouldBeTrue();

        // Act
        TextField(cut, "Cím").Find("input").Input(Title);
        var afterTitle = SuggestionButton(cut).HasAttribute("disabled");
        TextField(cut, "Leírás").Find("textarea").Input("Rövid");
        var afterShortDescription = SuggestionButton(cut).HasAttribute("disabled");
        TextField(cut, "Leírás").Find("textarea").Input(Description);

        // Assert
        afterTitle.ShouldBeTrue();
        afterShortDescription.ShouldBeTrue();
        SuggestionButton(cut).HasAttribute("disabled").ShouldBeFalse();
        cut.FindAll("#suggestion-hint").ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task RequestSuggestion_WhileRunning_DisablesButtonAndShowsProgress()
    {
        // Arrange
        var answer = new TaskCompletionSource<Result<CategorySuggestionDto>>();
        _runner.RunAsync(
                Arg.Any<Func<SuggestCategoryHandler, CancellationToken, Task<Result<CategorySuggestionDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(answer.Task);
        var cut = Render<NewTicket>();
        FillText(cut);

        // Act
        var click = SuggestionButton(cut).ClickAsync(new());

        // Assert
        SuggestionButton(cut).HasAttribute("disabled").ShouldBeTrue();
        SubmitButton(cut).HasAttribute("disabled").ShouldBeTrue();
        cut.FindAll("#suggestion-progress").ShouldHaveSingleItem();

        answer.SetResult(SuggestionOf(_network));
        await click;
        cut.WaitForAssertion(() => cut.FindAll("#suggestion-progress").ShouldBeEmpty());
        SuggestionButton(cut).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task RequestSuggestion_Success_ShowsCategoryConfidenceAndReasoningAsPlainText()
    {
        // Arrange
        const string reasoning = "A <b>wifi</b> nem működik,\na hálózati kategória illik rá.";
        SuggestionReturns(SuggestionOf(_network, confidence: 0.82, reasoning: reasoning));
        var cut = Render<NewTicket>();
        FillText(cut);

        // Act
        await SuggestionButton(cut).ClickAsync(new());

        // Assert
        cut.Find("#suggestion-category").TextContent.ShouldBe("Hálózat");
        cut.Find("#suggestion-confidence").TextContent.ShouldBe("Megbízhatóság: 82%");
        var reasoningElement = cut.Find("#suggestion-reasoning");
        reasoningElement.TextContent.ShouldBe(reasoning);
        reasoningElement.QuerySelector("b").ShouldBeNull(); // AGENTS.md §6: no MarkupString for LLM output
        cut.FindAll("#suggestion-stale").ShouldBeEmpty();
        Select<Guid?>(cut, "Kategória").Instance.GetState(x => x.Value).ShouldBeNull(); // BR-09: nothing is set without a decision
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task AcceptSuggestion_SetsSuggestedCategory()
    {
        // Arrange
        SuggestionReturns(SuggestionOf(_network));
        var cut = Render<NewTicket>();
        FillText(cut);
        await SuggestionButton(cut).ClickAsync(new());

        // Act
        await cut.Find("#suggestion-accept").ClickAsync(new());

        // Assert
        Select<Guid?>(cut, "Kategória").Instance.GetState(x => x.Value).ShouldBe(_network.Id);
        cut.Find("#suggestion-decision").TextContent.ShouldContain("elfogadásként");
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task ChooseOtherCategory_AfterAccept_ClearsSuggestedCategory()
    {
        // Arrange
        SuggestionReturns(SuggestionOf(_network));
        var cut = Render<NewTicket>();
        FillText(cut);
        await SuggestionButton(cut).ClickAsync(new());
        await cut.Find("#suggestion-accept").ClickAsync(new());

        // Act
        await cut.Find("#suggestion-other").ClickAsync(new());

        // Assert
        Select<Guid?>(cut, "Kategória").Instance.GetState(x => x.Value).ShouldBeNull();
        cut.FindAll("#suggestion-decision").ShouldBeEmpty();
    }

    [Theory]
    [Trait("BR", "BR-08")]
    [InlineData("Ai.NoSuggestion")]
    [InlineData("Ai.RateLimited")]
    public async Task RequestSuggestion_Unavailable_ShowsFriendlyMessageAndManualChoiceStillCreatesTicket(string errorCode)
    {
        // Arrange
        var error = errorCode == AiErrors.RateLimited.Code ? AiErrors.RateLimited : AiErrors.NoSuggestion;
        SuggestionReturns(error);
        var ticketId = Guid.CreateVersion7();
        CreateTicketReturns(ticketId);
        var cut = Render<NewTicket>();
        FillText(cut);

        // Act
        await SuggestionButton(cut).ClickAsync(new());
        await FillFormAsync(cut);
        await ClickSubmitAsync(cut);

        // Assert
        cut.Find("#suggestion-error").TextContent.Trim().ShouldBe(error.Message);
        cut.FindAll("#suggestion-card").ShouldBeEmpty();
        _snackbar.DidNotReceive().Add(error.Message, Arg.Any<Severity>(), Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe($"http://localhost/tickets/{ticketId}");
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task EditDescription_AfterSuggestion_ShowsStaleWarning()
    {
        // Arrange
        SuggestionReturns(SuggestionOf(_network));
        var cut = Render<NewTicket>();
        FillText(cut);
        await SuggestionButton(cut).ClickAsync(new());

        // Act
        TextField(cut, "Leírás").Find("textarea").Input(Description + " A nyomtató sem működik.");

        // Assert
        cut.Find("#suggestion-stale").TextContent.ShouldContain("elavulhatott");
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task Submit_AcceptedSuggestion_RecordsAcceptedDecisionWithTicket()
    {
        // Arrange: the real CreateTicket handler on replaced ports, so the test sees what the form sends.
        var suggestion = CategorySuggestion.Create(
            TestUsers.UserId, _network, 0.9, "Wifi-hiba.", "gemma3:4b", "v1", SuggestedAt).Value;
        var suggestions = Substitute.For<ICategorySuggestionRepository>();
        suggestions.GetByIdAsync(suggestion.Id, Arg.Any<CancellationToken>()).Returns(suggestion);
        RunRealCreateTicket(suggestions);
        SuggestionReturns(new CategorySuggestionDto(suggestion.Id, _network.Id, _network.Name, 0.9, "Wifi-hiba."));
        var cut = Render<NewTicket>();
        FillText(cut);
        await SuggestionButton(cut).ClickAsync(new());
        await cut.Find("#suggestion-accept").ClickAsync(new());
        await cut.InvokeAsync(() => Select<Impact?>(cut, "Hatás").Instance.SelectOption(Impact.Medium));
        await cut.InvokeAsync(() => Select<Urgency?>(cut, "Sürgősség").Instance.SelectOption(Urgency.Medium));

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        suggestion.Decision.ShouldBe(CategorySuggestionDecision.Accepted);
        var ticketId = suggestion.TicketId.ShouldNotBeNull();
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe($"http://localhost/tickets/{ticketId}");
    }

    private static ActiveCategoryDto DtoOf(Category category) =>
        new(category.Id, category.Code, category.Name, category.Description);

    private static CategorySuggestionDto SuggestionOf(
        Category category,
        double confidence = 0.75,
        string reasoning = "A leírás alapján ez a kategória illik rá.") =>
        new(Guid.CreateVersion7(), category.Id, category.Name, confidence, reasoning);

    private void SuggestionReturns(Result<CategorySuggestionDto> result) =>
        _runner.RunAsync(
                Arg.Any<Func<SuggestCategoryHandler, CancellationToken, Task<Result<CategorySuggestionDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(result);

    private void CreateTicketReturns(Result<Guid> result) =>
        _runner.RunAsync(
                Arg.Any<Func<CreateTicketHandler, CancellationToken, Task<Result<Guid>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(result);

    // A signed-in Reporter, with no eligible agent, so the ticket stays New (BR-06); the save succeeds.
    private void RunRealCreateTicket(ICategorySuggestionRepository suggestions)
    {
        var reporter = Substitute.For<ICurrentUser>();
        reporter.UserId.Returns(TestUsers.UserId);
        reporter.IsAuthenticated.Returns(true);
        reporter.IsInRole(Roles.Reporter).Returns(true);

        var categories = Substitute.For<ICategoryRepository>();
        categories.GetByIdAsync(_network.Id, Arg.Any<CancellationToken>()).Returns(_network);
        var agents = Substitute.For<IAgentRepository>();
        agents.GetEligibleCandidatesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<AgentCandidate>());
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var handler = new CreateTicketHandler(
            reporter,
            new CreateTicketValidator(),
            categories,
            suggestions,
            Substitute.For<ITicketRepository>(),
            new TicketAssignmentService(agents, new LeastLoadedAssignmentStrategy(), TimeProvider.System),
            unitOfWork,
            TimeProvider.System,
            NullLogger<CreateTicketHandler>.Instance);
        _runner.RunAsync(Arg.Any<Func<CreateTicketHandler, CancellationToken, Task<Result<Guid>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CreateTicketHandler, CancellationToken, Task<Result<Guid>>>>()(
                handler, call.Arg<CancellationToken>()));
    }

    // The text fields are Immediate, so they take the value from the input event.
    private static void FillText(IRenderedComponent<NewTicket> cut)
    {
        TextField(cut, "Cím").Find("input").Input(Title);
        TextField(cut, "Leírás").Find("textarea").Input(Description);
    }

    private async Task FillFormAsync(IRenderedComponent<NewTicket> cut)
    {
        FillText(cut);
        await cut.InvokeAsync(() => Select<Impact?>(cut, "Hatás").Instance.SelectOption(Impact.High));
        await cut.InvokeAsync(() => Select<Urgency?>(cut, "Sürgősség").Instance.SelectOption(Urgency.Medium));
        await cut.InvokeAsync(() => Select<Guid?>(cut, "Kategória").Instance.SelectOption(_hardware.Id));
    }

    private static AngleSharp.Dom.IElement SuggestionButton(IRenderedComponent<NewTicket> cut) => cut.Find("#suggestion-request");

    private static AngleSharp.Dom.IElement SubmitButton(IRenderedComponent<NewTicket> cut) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == SubmitText);

    private static Task ClickSubmitAsync(IRenderedComponent<NewTicket> cut) => SubmitButton(cut).ClickAsync(new());

    private static IRenderedComponent<MudTextField<string>> TextField(IRenderedComponent<NewTicket> cut, string label) =>
        cut.FindComponents<MudTextField<string>>().Single(field => field.Instance.Label == label);

    private static IRenderedComponent<MudSelect<T>> Select<T>(IRenderedComponent<NewTicket> cut, string label) =>
        cut.FindComponents<MudSelect<T>>().Single(select => select.Instance.Label == label);
}
