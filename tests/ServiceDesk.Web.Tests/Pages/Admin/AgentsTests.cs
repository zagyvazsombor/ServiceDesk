using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Agents.DeactivateAgent;
using ServiceDesk.Application.Agents.ListAgents;
using ServiceDesk.Application.Agents.SetAgentCompetencies;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Web.Components.MasterData;
using ServiceDesk.Web.Components.Pages.Admin;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages.Admin;

// The agent list with the use cases and the dialogs replaced. Anna is active with an inactive OTHER competency, Béla
// is inactive without competencies.
public sealed class AgentsTests : BunitContext
{
    private static readonly AgentCompetencyDto HardwareCompetency = new(Guid.CreateVersion7(), "HARDWARE", "Hardver", IsActive: true);
    private static readonly AgentCompetencyDto OtherCompetency = new(Guid.CreateVersion7(), "OTHER", "Egyéb", IsActive: false);

    private static readonly AgentListItemDto Anna =
        new(Guid.CreateVersion7(), "Kiss Anna", IsActive: true, [HardwareCompetency, OtherCompetency], Load: 7, OpenTicketCount: 2);

    private static readonly AgentListItemDto Bela =
        new(Guid.CreateVersion7(), "Nagy Béla", IsActive: false, [], Load: 0, OpenTicketCount: 0);

    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private int _loads;

    public AgentsTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_dialogService);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _runner.RunAsync(
                Arg.Any<Func<ListAgentsHandler, CancellationToken, Task<Result<IReadOnlyList<AgentListItemDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _loads++;
                return Result.Success<IReadOnlyList<AgentListItemDto>>([Anna, Bela]);
            });
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Render_Agents_ShowsStateCompetencyChipsAndLoad()
    {
        // Act
        var cut = Render<Agents>();

        // Assert
        var rows = cut.FindAll("tbody tr");
        rows.Count.ShouldBe(2);
        Cells(rows[0]).Take(5).ShouldBe(["Kiss Anna", "Aktív", "HardverEgyéb (inaktív)", "7", "2"]);
        Cells(rows[1]).Take(5).ShouldBe(["Nagy Béla", "Inaktív", "Nincs kompetencia", "0", "0"]);
        cut.FindAll($"#deactivate-agent-{Anna.Id}").Count.ShouldBe(1);
        cut.FindAll($"#activate-agent-{Bela.Id}").Count.ShouldBe(1);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public async Task Deactivate_NotConfirmed_DoesNotRunDeactivateAgent()
    {
        // Arrange
        ConfirmationReturns(null);
        var cut = Render<Agents>();

        // Act
        await cut.Find($"#deactivate-agent-{Anna.Id}").ClickAsync(new());

        // Assert: the confirmation says that the assigned tickets stay with the agent.
        await _dialogService.Received(1).ShowMessageBoxAsync(
            "Ügyintéző inaktiválása",
            Arg.Is<string>(message => message.Contains("(2 nyitott jegy) nála maradnak", StringComparison.Ordinal)),
            "Inaktiválás",
            Arg.Any<string?>(),
            "Mégse",
            Arg.Any<DialogOptions?>());
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<DeactivateAgentHandler, Result>(default!, Xunit.TestContext.Current.CancellationToken);
        _loads.ShouldBe(1);
    }

    [Fact]
    [Trait("BR", "BR-12")]
    public async Task Deactivate_Confirmed_RunsDeactivateAgentAndReloads()
    {
        // Arrange
        ConfirmationReturns(true);
        CommandReturns<DeactivateAgentHandler>(Result.Success());
        var cut = Render<Agents>();

        // Act
        await cut.Find($"#deactivate-agent-{Anna.Id}").ClickAsync(new());

        // Assert
        await _runner.Received(1).RunAsync(
            Arg.Any<Func<DeactivateAgentHandler, CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
        _snackbar.Received(1).Add("Az ügyintéző inaktiválva.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _loads.ShouldBe(2);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task EditCompetencies_DialogOk_PassesActiveCategoriesAndRunsSetAgentCompetencies()
    {
        // Arrange
        IReadOnlyList<ActiveCategoryDto> categories =
        [
            new(HardwareCompetency.CategoryId, "HARDWARE", "Hardver", "Eszközök."),
            new(Guid.CreateVersion7(), "NETWORK", "Hálózat", "Hálózat és VPN."),
        ];
        _runner.RunAsync(
                Arg.Any<Func<GetActiveCategoriesHandler, CancellationToken, Task<Result<IReadOnlyList<ActiveCategoryDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(categories));
        DialogReturns<AgentCompetenciesDialog>(DialogResult.Ok<IReadOnlyList<Guid>>([categories[1].Id]));
        CommandReturns<SetAgentCompetenciesHandler>(Result.Success());
        var cut = Render<Agents>();

        // Act
        await cut.Find($"#edit-competencies-{Anna.Id}").ClickAsync(new());

        // Assert
        await _dialogService.Received(1).ShowAsync<AgentCompetenciesDialog>(
            "Kiss Anna kompetenciái",
            Arg.Is<DialogParameters>(parameters =>
                ReferenceEquals(parameters[nameof(AgentCompetenciesDialog.Categories)], categories)
                && ReferenceEquals(parameters[nameof(AgentCompetenciesDialog.Competencies)], Anna.Competencies)),
            Arg.Any<DialogOptions>());
        await _runner.Received(1).RunAsync(
            Arg.Any<Func<SetAgentCompetenciesHandler, CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
        _snackbar.Received(1).Add("A kompetenciák mentve.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _loads.ShouldBe(2);
    }

    [Fact]
    public async Task EditCompetencies_DialogCanceled_DoesNotRunSetAgentCompetencies()
    {
        // Arrange
        _runner.RunAsync(
                Arg.Any<Func<GetActiveCategoriesHandler, CancellationToken, Task<Result<IReadOnlyList<ActiveCategoryDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ActiveCategoryDto>>([]));
        DialogReturns<AgentCompetenciesDialog>(DialogResult.Cancel());
        var cut = Render<Agents>();

        // Act
        await cut.Find($"#edit-competencies-{Bela.Id}").ClickAsync(new());

        // Assert
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<SetAgentCompetenciesHandler, Result>(default!, Xunit.TestContext.Current.CancellationToken);
        _loads.ShouldBe(1);
    }

    private static List<string> Cells(AngleSharp.Dom.IElement row) =>
        row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToList();

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
}
