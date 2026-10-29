using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Categories.ActivateCategory;
using ServiceDesk.Application.Categories.DeactivateCategory;
using ServiceDesk.Application.Categories.ListCategories;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Web.Components.MasterData;
using ServiceDesk.Web.Components.Pages.Admin;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages.Admin;

// The category list with the use cases and the dialogs replaced.
public sealed class CategoriesTests : BunitContext
{
    private static readonly CategoryListItemDto Hardware =
        new(Guid.CreateVersion7(), "HARDWARE", "Hardver", "Számítógépek, perifériák és egyéb eszközök hibái.", IsActive: true);

    private static readonly CategoryListItemDto Other =
        new(Guid.CreateVersion7(), "OTHER", "Egyéb", "Minden más.", IsActive: false);

    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private int _loads;

    public CategoriesTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_dialogService);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _runner.RunAsync(
                Arg.Any<Func<ListCategoriesHandler, CancellationToken, Task<Result<IReadOnlyList<CategoryListItemDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _loads++;
                return Result.Success<IReadOnlyList<CategoryListItemDto>>([Hardware, Other]);
            });
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Render_Categories_ListsActiveAndInactiveWithoutDeleteAction()
    {
        // Act
        var cut = Render<Categories>();

        // Assert
        var rows = cut.FindAll("tbody tr");
        rows.Count.ShouldBe(2);
        Cells(rows[0]).Take(4).ShouldBe(["HARDWARE", "Hardver", Hardware.Description, "Aktív"]);
        Cells(rows[1]).Take(4).ShouldBe(["OTHER", "Egyéb", "Minden más.", "Inaktív"]);
        cut.FindAll("#deactivate-category-HARDWARE").Count.ShouldBe(1);
        cut.FindAll("#activate-category-HARDWARE").ShouldBeEmpty();
        cut.FindAll("#activate-category-OTHER").Count.ShouldBe(1);
        cut.FindAll("#deactivate-category-OTHER").ShouldBeEmpty();
        cut.FindAll("button").Select(button => button.TextContent.Trim()).ShouldNotContain("Törlés");
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task Deactivate_NotConfirmed_DoesNotRunDeactivateCategory()
    {
        // Arrange
        ConfirmationReturns(null);
        var cut = Render<Categories>();

        // Act
        await cut.Find("#deactivate-category-HARDWARE").ClickAsync(new());

        // Assert
        await _dialogService.Received(1).ShowMessageBoxAsync(
            "Kategória inaktiválása",
            Arg.Is<string>(message => message.Contains("A meglévő jegyek", StringComparison.Ordinal)),
            "Inaktiválás",
            Arg.Any<string?>(),
            "Mégse",
            Arg.Any<DialogOptions?>());
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<DeactivateCategoryHandler, Result>(default!, Xunit.TestContext.Current.CancellationToken);
        _loads.ShouldBe(1);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task Deactivate_Confirmed_RunsDeactivateCategoryAndReloads()
    {
        // Arrange
        ConfirmationReturns(true);
        CommandReturns<DeactivateCategoryHandler>(Result.Success());
        var cut = Render<Categories>();

        // Act
        await cut.Find("#deactivate-category-HARDWARE").ClickAsync(new());

        // Assert
        await _runner.Received(1).RunAsync(
            Arg.Any<Func<DeactivateCategoryHandler, CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
        _snackbar.Received(1).Add("A kategória inaktiválva.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _loads.ShouldBe(2);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task Activate_NotConfirmed_DoesNotRunActivateCategory()
    {
        // Arrange
        ConfirmationReturns(false);
        var cut = Render<Categories>();

        // Act
        await cut.Find("#activate-category-OTHER").ClickAsync(new());

        // Assert
        await _dialogService.Received(1).ShowMessageBoxAsync(
            "Kategória aktiválása", Arg.Any<string>(), "Aktiválás", Arg.Any<string?>(), "Mégse", Arg.Any<DialogOptions?>());
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<ActivateCategoryHandler, Result>(default!, Xunit.TestContext.Current.CancellationToken);
        _loads.ShouldBe(1);
    }

    [Fact]
    public async Task Deactivate_UseCaseRefuses_ShowsErrorSnackbarWithoutReload()
    {
        // Arrange
        ConfirmationReturns(true);
        CommandReturns<DeactivateCategoryHandler>(CategoryErrors.NotFound);
        var cut = Render<Categories>();

        // Act
        await cut.Find("#deactivate-category-HARDWARE").ClickAsync(new());

        // Assert
        _snackbar.Received(1).Add(CategoryErrors.NotFound.Message, Severity.Error, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _loads.ShouldBe(1);
    }

    [Fact]
    public async Task Create_DialogSucceeded_ShowsSnackbarAndReloads()
    {
        // Arrange
        DialogReturns<CategoryDialog>(DialogResult.Ok(true));
        var cut = Render<Categories>();

        // Act
        await cut.Find("#create-category").ClickAsync(new());

        // Assert
        await _dialogService.Received(1).ShowAsync<CategoryDialog>(
            "Új kategória",
            Arg.Is<DialogParameters>(parameters => parameters[nameof(CategoryDialog.EditedCategory)] == null),
            Arg.Any<DialogOptions>());
        _snackbar.Received(1).Add("A kategória létrehozva.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        _loads.ShouldBe(2);
    }

    [Fact]
    public async Task Edit_DialogCanceled_PassesCategoryAndDoesNotReload()
    {
        // Arrange
        DialogReturns<CategoryDialog>(DialogResult.Cancel());
        var cut = Render<Categories>();

        // Act
        await cut.Find("#edit-category-OTHER").ClickAsync(new());

        // Assert
        await _dialogService.Received(1).ShowAsync<CategoryDialog>(
            "Kategória szerkesztése",
            Arg.Is<DialogParameters>(parameters => ReferenceEquals(parameters[nameof(CategoryDialog.EditedCategory)], Other)),
            Arg.Any<DialogOptions>());
        _snackbar.DidNotReceiveWithAnyArgs().Add(default(string)!, default, default, default);
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
