using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Categories.CreateCategory;
using ServiceDesk.Application.Categories.ListCategories;
using ServiceDesk.Application.Categories.UpdateCategory;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Web.Components.MasterData;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Components.MasterData;

// The dialog runs the real CreateCategory and UpdateCategory handlers on replaced ports, so the tests see the command
// and the answer of the real validator.
public sealed class CategoryDialogTests : BunitContext
{
    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly List<Category> _added = [];

    public CategoryDialogTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var admin = Substitute.For<ICurrentUser>();
        admin.UserId.Returns(TestUsers.UserId);
        admin.IsAuthenticated.Returns(true);
        admin.IsInRole(Roles.Admin).Returns(true);

        _categories.Add(Arg.Do<Category>(_added.Add));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var createHandler = new CreateCategoryHandler(
            admin, new CreateCategoryValidator(_categories), _categories, _unitOfWork, NullLogger<CreateCategoryHandler>.Instance);
        _runner.RunAsync(Arg.Any<Func<CreateCategoryHandler, CancellationToken, Task<Result<Guid>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CreateCategoryHandler, CancellationToken, Task<Result<Guid>>>>()(
                createHandler, call.Arg<CancellationToken>()));

        var updateHandler = new UpdateCategoryHandler(
            admin, new UpdateCategoryValidator(), _categories, _unitOfWork, NullLogger<UpdateCategoryHandler>.Instance);
        _runner.RunAsync(Arg.Any<Func<UpdateCategoryHandler, CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<UpdateCategoryHandler, CancellationToken, Task<Result>>>()(
                updateHandler, call.Arg<CancellationToken>()));
    }

    [Fact]
    public async Task Submit_NewCategory_RunsCreateCategoryAndClosesWithOk()
    {
        // Arrange
        var cut = RenderProviders();
        var dialog = await ShowAsync(cut, editedCategory: null);

        // Act
        TextField(cut, "Kód").Find("input").Change("PRINTER");
        TextField(cut, "Név").Find("input").Change("Nyomtató");
        TextField(cut, "Leírás").Find("textarea").Change("Nyomtatók hibái.");
        await cut.Find("#category-submit").ClickAsync(new());

        // Assert
        var result = await dialog.Result;
        result.ShouldNotBeNull().Canceled.ShouldBeFalse();
        var category = _added.ShouldHaveSingleItem();
        (category.Code, category.Name, category.Description, category.IsActive)
            .ShouldBe(("PRINTER", "Nyomtató", "Nyomtatók hibái.", true));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_DuplicateCode_ShowsErrorAtCodeFieldAndStaysOpen()
    {
        // Arrange
        _categories.CodeExistsAsync("HARDWARE", Arg.Any<CancellationToken>()).Returns(true);
        var cut = RenderProviders();
        var dialog = await ShowAsync(cut, editedCategory: null);
        TextField(cut, "Kód").Find("input").Change("HARDWARE");
        TextField(cut, "Név").Find("input").Change("Hardver");

        // Act
        await cut.Find("#category-submit").ClickAsync(new());

        // Assert
        var code = TextField(cut, "Kód");
        code.Instance.GetState(x => x.Error).ShouldBeTrue();
        code.Instance.GetState(x => x.ErrorText).ShouldBe(CategoryErrors.CodeAlreadyExists.Message);
        TextField(cut, "Név").Instance.GetState(x => x.Error).ShouldBeFalse();
        code.Instance.GetState(x => x.Value).ShouldBe("HARDWARE");
        dialog.Result.IsCompleted.ShouldBeFalse();
        _added.ShouldBeEmpty();
        _snackbar.DidNotReceiveWithAnyArgs().Add(default(string)!, default, default, default);
    }

    [Fact]
    public async Task Submit_ExistingCategory_KeepsCodeDisabledAndRunsUpdateCategory()
    {
        // Arrange
        var category = Category.Create("HARDWARE", "Hardver", "Eszközök.").Value;
        _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        var cut = RenderProviders();
        var dialog = await ShowAsync(cut, new CategoryListItemDto(category.Id, category.Code, category.Name, category.Description, IsActive: true));

        // Assert: AGENTS.md §8, the code is shown but cannot be changed.
        var code = TextField(cut, "Kód");
        code.Instance.Disabled.ShouldBeTrue();
        code.Instance.GetState(x => x.Value).ShouldBe("HARDWARE");

        // Act
        TextField(cut, "Név").Find("input").Change("Hardver és perifériák");
        await cut.Find("#category-submit").ClickAsync(new());

        // Assert
        var result = await dialog.Result;
        result.ShouldNotBeNull().Canceled.ShouldBeFalse();
        (category.Code, category.Name, category.Description).ShouldBe(("HARDWARE", "Hardver és perifériák", "Eszközök."));
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<CreateCategoryHandler, Result<Guid>>(default!, Xunit.TestContext.Current.CancellationToken);
    }

    // The dialog provider shows the dialog, the popover provider the counters and helper texts of the fields.
    private IRenderedComponent<ContainerFragment> RenderProviders() =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
        });

    private Task<IDialogReference> ShowAsync(IRenderedComponent<ContainerFragment> cut, CategoryListItemDto? editedCategory)
    {
        var parameters = new DialogParameters<CategoryDialog> { { dialog => dialog.EditedCategory, editedCategory } };
        var dialogService = Services.GetRequiredService<IDialogService>();
        return cut.InvokeAsync(() => dialogService.ShowAsync<CategoryDialog>("Kategória", parameters));
    }

    private static IRenderedComponent<MudTextField<string>> TextField(IRenderedComponent<ContainerFragment> cut, string label) =>
        cut.FindComponents<MudTextField<string>>().Single(field => field.Instance.Label == label);
}
