using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Web.Components.Tickets;
using Shouldly;

namespace ServiceDesk.Web.Tests.Components.Tickets;

public sealed class RecategorizeTicketDialogTests : BunitContext
{
    private static readonly ActiveCategoryDto Hardware =
        new(Guid.CreateVersion7(), "HARDWARE", "Hardver", "Számítógépek, perifériák és egyéb eszközök.");

    private static readonly ActiveCategoryDto Network =
        new(Guid.CreateVersion7(), "NETWORK", "Hálózat", "Hálózat, wifi és VPN.");

    public RecategorizeTicketDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Render_ActiveCurrentCategory_IsPreselectedAndSubmitDisabled()
    {
        // Arrange
        var cut = RenderProvider();

        // Act
        await ShowAsync(cut, currentCategoryCode: "HARDWARE");

        // Assert
        cut.FindComponent<MudSelect<Guid?>>().Instance.GetState(x => x.Value).ShouldBe(Hardware.Id);
        cut.Markup.ShouldContain(Hardware.Description);
        cut.Find("#recategorize-submit").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public async Task Submit_OtherCategorySelected_ClosesWithCategoryId()
    {
        // Arrange
        var cut = RenderProvider();
        var dialog = await ShowAsync(cut, currentCategoryCode: "HARDWARE");

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<Guid?>>().Instance.SelectOption(Network.Id));
        await cut.Find("#recategorize-submit").ClickAsync(new());

        // Assert
        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();
        result.Data.ShouldBe(Network.Id);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task Render_InactiveCurrentCategory_SelectsNothing()
    {
        // Arrange
        var cut = RenderProvider();

        // Act: the OTHER category of the ticket was deactivated, so it is not among the active ones.
        await ShowAsync(cut, currentCategoryCode: "OTHER");

        // Assert
        cut.FindComponent<MudSelect<Guid?>>().Instance.GetState(x => x.Value).ShouldBeNull();
        cut.Find("#recategorize-submit").HasAttribute("disabled").ShouldBeTrue();
    }

    private IRenderedComponent<ContainerFragment> RenderProvider() =>
        Render(builder =>
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
        });

    private Task<IDialogReference> ShowAsync(IRenderedComponent<ContainerFragment> cut, string currentCategoryCode)
    {
        var parameters = new DialogParameters<RecategorizeTicketDialog>
        {
            { dialog => dialog.Categories, [Hardware, Network] },
            { dialog => dialog.CurrentCategoryCode, currentCategoryCode },
        };
        var dialogService = Services.GetRequiredService<IDialogService>();
        return cut.InvokeAsync(() => dialogService.ShowAsync<RecategorizeTicketDialog>("Átkategorizálás", parameters));
    }
}
