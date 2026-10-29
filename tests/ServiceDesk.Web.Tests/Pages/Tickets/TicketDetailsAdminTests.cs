using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.AssignTicketManually;
using ServiceDesk.Application.Tickets.ChangeImpactAndUrgency;
using ServiceDesk.Application.Tickets.CloseTicket;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.GetAssignableAgents;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Application.Tickets.RecategorizeTicket;
using ServiceDesk.Application.Tickets.RetryAutoAssign;
using ServiceDesk.Application.Tickets.StartWork;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Web.Components.Pages.Tickets;
using ServiceDesk.Web.Components.Tickets;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages.Tickets;

// The Admin panel of the ticket details page (TicketAdminPanel) with the use cases replaced.
public sealed class TicketDetailsAdminTests : BunitContext
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly BunitAuthorizationContext _authorization;
    private int _detailsLoads;

    public TicketDetailsAdminTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_dialogService);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _authorization = AddAuthorization();
        _authorization.SetAuthorized("admin.anna@servicedesk.local");
        _authorization.SetRoles(Roles.Admin);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public void Render_ClosedTicket_HidesAssignmentAndStatusButtons()
    {
        // Arrange
        var ticket = Details(TicketStatus.Closed, assigneeName: "Kiss Anna");
        DetailsReturn(ticket);

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        cut.Find("#ticket-closed").TextContent.ShouldContain("A jegy lezárva");
        ActionIds(cut).ShouldBeEmpty();
        cut.FindComponents<MudSelect<Impact>>().ShouldBeEmpty();
    }

    [Theory]
    [Trait("BR", "BR-03")]
    [Trait("BR", "BR-06")]
    [Trait("BR", "BR-07")]
    [InlineData(TicketStatus.New, null, new[] { "action-save-triage", "action-assign", "action-recategorize", "action-retry-auto-assign" })]
    [InlineData(TicketStatus.Assigned, "Kiss Anna", new[] { "action-save-triage", "action-start", "action-assign", "action-recategorize" })]
    [InlineData(TicketStatus.InProgress, "Kiss Anna", new[] { "action-save-triage", "action-resolve", "action-assign", "action-recategorize" })]
    [InlineData(TicketStatus.Resolved, "Kiss Anna", new[] { "action-save-triage", "action-reopen", "action-close", "action-assign" })]
    public void Render_OpenTicket_ShowsOnlyAllowedActions(TicketStatus status, string? assigneeName, string[] expected)
    {
        // Arrange
        var ticket = Details(status, assigneeName);
        DetailsReturn(ticket);

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        ActionIds(cut).ShouldBe(expected);
        cut.Find("#action-assign").TextContent.Trim().ShouldBe(assigneeName is null ? "Kiosztás" : "Átosztás");
        cut.FindAll("#ticket-closed").ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public void Render_Reporter_DoesNotShowAdminPanel()
    {
        // Arrange
        _authorization.SetRoles(Roles.Reporter);
        var ticket = Details(TicketStatus.Assigned, assigneeName: "Kiss Anna");
        DetailsReturn(ticket);

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        cut.Find("#ticket-title").TextContent.ShouldBe(ticket.Title);
        cut.FindAll("#admin-panel").ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public void Render_AdminWithAcceptedSuggestion_ShowsSuggestionConfidenceDecisionAndReasoning()
    {
        // Arrange
        const string reasoning = "A <i>wifi</i> a hálózathoz tartozik.";
        var ticket = Details(TicketStatus.Assigned, assigneeName: "Kiss Anna") with
        {
            CategorySuggestion = new TicketCategorySuggestionDto(
                "Hálózat", 0.9, reasoning, CategorySuggestionDecision.Accepted, "gemma3:4b", "v1", CreatedAt),
        };
        DetailsReturn(ticket);

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        cut.Find("#ai-suggestion-category").TextContent.ShouldBe("Javasolt kategória: Hálózat");
        cut.Find("#ai-suggestion-confidence").TextContent.ShouldBe("Megbízhatóság: 90%");
        cut.Find("#ai-suggestion-decision").TextContent.ShouldContain("Elfogadva");
        var reasoningElement = cut.Find("#ai-suggestion-reasoning");
        reasoningElement.TextContent.ShouldBe(reasoning);
        reasoningElement.QuerySelector("i").ShouldBeNull(); // AGENTS.md §6: no MarkupString for LLM output
        cut.Find("#ai-suggestion-info").TextContent.ShouldContain("gemma3:4b");
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public void Render_AdminWithOverriddenSuggestion_ShowsOverridden()
    {
        // Arrange
        var ticket = Details(TicketStatus.New) with
        {
            CategorySuggestion = new TicketCategorySuggestionDto(
                "Szoftver", 0.4, "Alkalmazáshiba.", CategorySuggestionDecision.Overridden, "gemma3:4b", "v1", CreatedAt),
        };
        DetailsReturn(ticket);

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        cut.Find("#ai-suggestion-decision").TextContent.ShouldContain("Felülbírálva");
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public void Render_AdminWithoutSuggestion_ShowsThatTicketHasNoSuggestion()
    {
        // Arrange
        var ticket = Details(TicketStatus.New);
        DetailsReturn(ticket);

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        cut.Find("#ai-suggestion-none").TextContent.ShouldContain("nem tartozik AI-javaslat");
        cut.FindAll("#ai-suggestion-decision").ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public void Render_Reporter_DoesNotShowSuggestionInfo()
    {
        // Arrange
        _authorization.SetRoles(Roles.Reporter);
        var ticket = Details(TicketStatus.Assigned, assigneeName: "Kiss Anna") with
        {
            CategorySuggestion = new TicketCategorySuggestionDto(
                "Hálózat", 0.9, "Wifi.", CategorySuggestionDecision.Accepted, "gemma3:4b", "v1", CreatedAt),
        };
        DetailsReturn(ticket);

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        cut.Find("#ticket-title").TextContent.ShouldBe(ticket.Title);
        cut.FindAll("#ai-suggestion-info").ShouldBeEmpty();
    }

    [Fact]
    public async Task StartWork_ConcurrencyConflict_ShowsSnackbarWithRefreshActionThatReloadsTicket()
    {
        // Arrange
        var ticket = Details(TicketStatus.Assigned, assigneeName: "Kiss Anna");
        DetailsReturn(ticket, ticket with { Status = TicketStatus.InProgress });
        CommandReturns<StartWorkHandler>(GeneralErrors.ConcurrencyConflict);
        Action<SnackbarOptions>? configure = null;
        _snackbar.Add(
            GeneralErrors.ConcurrencyConflict.Message,
            Severity.Warning,
            Arg.Do<Action<SnackbarOptions>>(options => configure = options),
            Arg.Any<string>());
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-start").ClickAsync(new());

        // Assert
        _snackbar.Received(1).Add(
            "A jegyet időközben módosították, frissítsd az oldalt.", Severity.Warning, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _detailsLoads.ShouldBe(1);
        configure.ShouldNotBeNull();
        var options = new SnackbarOptions(Severity.Warning, new SnackbarConfiguration());
        configure(options);
        options.Action.ShouldBe("Frissítés");
        options.RequireInteraction.ShouldBe(true);

        // Act: the "Frissítés" action of the Snackbar.
        await cut.InvokeAsync(() => options.OnClick!(null!));

        // Assert
        _detailsLoads.ShouldBe(2);
        cut.WaitForAssertion(() => ActionIds(cut).ShouldContain("action-resolve"));
    }

    [Fact]
    public async Task StartWork_UseCaseRefuses_ShowsErrorSnackbarWithoutReload()
    {
        // Arrange: the page still shows Assigned, but the use case finds another status (AGENTS.md §10).
        var ticket = Details(TicketStatus.Assigned, assigneeName: "Kiss Anna");
        DetailsReturn(ticket);
        CommandReturns<StartWorkHandler>(TicketErrors.InvalidTransition);
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-start").ClickAsync(new());

        // Assert
        _snackbar.Received(1).Add(TicketErrors.InvalidTransition.Message, Severity.Error, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _detailsLoads.ShouldBe(1);
    }

    [Fact]
    public async Task StartWork_Success_ShowsSnackbarAndReloadsTicket()
    {
        // Arrange
        var ticket = Details(TicketStatus.Assigned, assigneeName: "Kiss Anna");
        DetailsReturn(ticket, ticket with { Status = TicketStatus.InProgress });
        CommandReturns<StartWorkHandler>(Result.Success());
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-start").ClickAsync(new());

        // Assert
        _snackbar.Received(1).Add("A munka megkezdődött.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _detailsLoads.ShouldBe(2);
        cut.WaitForAssertion(() => ActionIds(cut).ShouldContain("action-resolve"));
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task Close_NotConfirmed_DoesNotRunCloseTicket()
    {
        // Arrange
        var ticket = Details(TicketStatus.Resolved, assigneeName: "Kiss Anna");
        DetailsReturn(ticket);
        ConfirmationReturns(null);
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-close").ClickAsync(new());

        // Assert
        await _dialogService.Received(1).ShowMessageBoxAsync(
            "Jegy lezárása", Arg.Any<string>(), "Lezárás", Arg.Any<string?>(), "Mégse", Arg.Any<DialogOptions?>());
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<CloseTicketHandler, Result>(default!, Xunit.TestContext.Current.CancellationToken);
        _detailsLoads.ShouldBe(1);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task Close_Confirmed_RunsCloseTicketAndReloads()
    {
        // Arrange
        var ticket = Details(TicketStatus.Resolved, assigneeName: "Kiss Anna");
        DetailsReturn(ticket, ticket with { Status = TicketStatus.Closed });
        ConfirmationReturns(true);
        CommandReturns<CloseTicketHandler>(Result.Success());
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-close").ClickAsync(new());

        // Assert
        _snackbar.Received(1).Add("A jegy lezárva.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _detailsLoads.ShouldBe(2);
        cut.WaitForAssertion(() => cut.Find("#ticket-closed"));
        ActionIds(cut).ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task Triage_HighImpact_ShowsPriorityPreviewAndRunsUseCase()
    {
        // Arrange: Medium × Medium is Medium.
        var ticket = Details(TicketStatus.Assigned, assigneeName: "Kiss Anna");
        DetailsReturn(ticket);
        CommandReturns<ChangeImpactAndUrgencyHandler>(Result.Success());
        var cut = RenderDetails(ticket.Id);
        cut.Find("#action-save-triage").HasAttribute("disabled").ShouldBeTrue();

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<Impact>>().Instance.SelectOption(Impact.High));

        // Assert: High × Medium is High, from PriorityMatrix.
        cut.Find("#priority-preview").TextContent.Trim().ShouldBe("Magas");
        var save = cut.Find("#action-save-triage");
        save.HasAttribute("disabled").ShouldBeFalse();

        // Act
        await save.ClickAsync(new());

        // Assert
        await _runner.Received(1).RunAsync(
            Arg.Any<Func<ChangeImpactAndUrgencyHandler, CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
        _snackbar.Received(1).Add("A besorolás mentve.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task Assign_AgentChosenInDialog_ShowsCandidatesAndRunsAssignment()
    {
        // Arrange
        var ticket = Details(TicketStatus.Assigned, assigneeName: "Kiss Anna");
        DetailsReturn(ticket);
        IReadOnlyList<AssignableAgentDto> agents =
        [
            new(Guid.CreateVersion7(), "Tóth Csaba", Load: 0, OpenTicketCount: 0, IsCurrentAssignee: false),
            new(Guid.CreateVersion7(), "Kiss Anna", Load: 3, OpenTicketCount: 1, IsCurrentAssignee: true),
        ];
        _runner.RunAsync(
                Arg.Any<Func<GetAssignableAgentsHandler, CancellationToken, Task<Result<AssignableAgentsDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AssignableAgentsDto(TicketStatus.Assigned, agents)));
        DialogReturns<AssignTicketDialog>(DialogResult.Ok(agents[0].AgentId));
        CommandReturns<AssignTicketManuallyHandler>(Result.Success());
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-assign").ClickAsync(new());

        // Assert
        await _dialogService.Received(1).ShowAsync<AssignTicketDialog>(
            "Átosztás",
            Arg.Is<DialogParameters>(parameters => ReferenceEquals(parameters[nameof(AssignTicketDialog.Agents)], agents)),
            Arg.Any<DialogOptions>());
        await _runner.Received(1).RunAsync(
            Arg.Any<Func<AssignTicketManuallyHandler, CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
        _snackbar.Received(1).Add("A jegy átosztva.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _detailsLoads.ShouldBe(2);
    }

    [Fact]
    public async Task Assign_DialogCanceled_DoesNotRunAssignment()
    {
        // Arrange
        var ticket = Details(TicketStatus.New);
        DetailsReturn(ticket);
        _runner.RunAsync(
                Arg.Any<Func<GetAssignableAgentsHandler, CancellationToken, Task<Result<AssignableAgentsDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AssignableAgentsDto(TicketStatus.New, [])));
        DialogReturns<AssignTicketDialog>(DialogResult.Cancel());
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-assign").ClickAsync(new());

        // Assert
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<AssignTicketManuallyHandler, Result>(default!, Xunit.TestContext.Current.CancellationToken);
        _detailsLoads.ShouldBe(1);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task Assign_CandidatesRefused_ShowsSnackbarWithoutDialog()
    {
        // Arrange: the ticket was closed since the page was loaded.
        var ticket = Details(TicketStatus.Resolved, assigneeName: "Kiss Anna");
        DetailsReturn(ticket);
        _runner.RunAsync(
                Arg.Any<Func<GetAssignableAgentsHandler, CancellationToken, Task<Result<AssignableAgentsDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(TicketErrors.ClosedCannotBeAssigned);
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-assign").ClickAsync(new());

        // Assert
        _snackbar.Received(1).Add(TicketErrors.ClosedCannotBeAssigned.Message, Severity.Error, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        await _dialogService.DidNotReceiveWithAnyArgs().ShowAsync<AssignTicketDialog>(default, default!, default);
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task Recategorize_CategoryChosenInDialog_PassesActiveCategoriesAndRunsRecategorization()
    {
        // Arrange
        var ticket = Details(TicketStatus.InProgress, assigneeName: "Kiss Anna");
        DetailsReturn(ticket);
        IReadOnlyList<ActiveCategoryDto> categories =
        [
            new(Guid.CreateVersion7(), "HARDWARE", "Hardver", "Eszközök."),
            new(Guid.CreateVersion7(), "NETWORK", "Hálózat", "Hálózat és VPN."),
        ];
        _runner.RunAsync(
                Arg.Any<Func<GetActiveCategoriesHandler, CancellationToken, Task<Result<IReadOnlyList<ActiveCategoryDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(categories));
        DialogReturns<RecategorizeTicketDialog>(DialogResult.Ok(categories[1].Id));
        CommandReturns<RecategorizeTicketHandler>(Result.Success());
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-recategorize").ClickAsync(new());

        // Assert
        await _dialogService.Received(1).ShowAsync<RecategorizeTicketDialog>(
            "Átkategorizálás",
            Arg.Is<DialogParameters>(parameters =>
                ReferenceEquals(parameters[nameof(RecategorizeTicketDialog.Categories)], categories)
                && (string?)parameters[nameof(RecategorizeTicketDialog.CurrentCategoryCode)] == "HARDWARE"),
            Arg.Any<DialogOptions>());
        _snackbar.Received(1).Add("A jegy átkategorizálva.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _detailsLoads.ShouldBe(2);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task RetryAutoAssign_NobodyEligible_ShowsInfoSnackbarWithoutReload()
    {
        // Arrange
        var ticket = Details(TicketStatus.New);
        DetailsReturn(ticket);
        _runner.RunAsync(
                Arg.Any<Func<RetryAutoAssignHandler, CancellationToken, Task<Result<Guid?>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<Guid?>(null));
        var cut = RenderDetails(ticket.Id);

        // Act
        await cut.Find("#action-retry-auto-assign").ClickAsync(new());

        // Assert
        _snackbar.Received(1).Add(
            "Továbbra sincs jogosult ügyintéző, a jegy kiosztatlan marad.", Severity.Info, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _detailsLoads.ShouldBe(1);
    }

    private IRenderedComponent<TicketDetails> RenderDetails(Guid id) =>
        Render<TicketDetails>(parameters => parameters.Add(page => page.Id, id));

    // The ids of the panel buttons, in the order they are shown.
    private static List<string> ActionIds(IRenderedComponent<TicketDetails> cut) =>
        cut.FindAll("button[id^='action-']").Select(button => button.Id!).ToList();

    // Answers the loads in order, repeating the last answer; counts them.
    private void DetailsReturn(params TicketDetailsDto[] answers)
    {
        _runner.RunAsync(
                Arg.Any<Func<GetTicketDetailsHandler, CancellationToken, Task<Result<TicketDetailsDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var answer = answers[Math.Min(_detailsLoads, answers.Length - 1)];
                _detailsLoads++;
                return Result.Success(answer);
            });
    }

    private void CommandReturns<THandler>(Result result)
        where THandler : notnull =>
        _runner.RunAsync(Arg.Any<Func<THandler, CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>())
            .Returns(result);

    private void ConfirmationReturns(bool? answer) =>
        _dialogService.ShowMessageBoxAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DialogOptions?>())
            .Returns(answer);

    private void DialogReturns<TDialog>(DialogResult result)
        where TDialog : IComponent
    {
        var reference = Substitute.For<IDialogReference>();
        reference.Result.Returns(Task.FromResult<DialogResult?>(result));
        _dialogService.ShowAsync<TDialog>(Arg.Any<string>(), Arg.Any<DialogParameters>(), Arg.Any<DialogOptions>())
            .Returns(reference);
    }

    private static TicketDetailsDto Details(TicketStatus status, string? assigneeName = null) =>
        new(
            Guid.CreateVersion7(),
            "Nem indul a laptop",
            "Bekapcsoláskor a laptop képernyője fekete marad.",
            Impact.Medium,
            Urgency.Medium,
            Priority.Medium,
            status,
            CreatedAt,
            TestUsers.Email,
            "HARDWARE",
            "Hardver",
            assigneeName,
            [new TicketHistoryEntryDto(1, CreatedAt, TicketHistoryAction.Created, TestUsers.Email, null, "New")]);
}
