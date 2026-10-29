using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using ServiceDesk.Application.Agents.ListAgents;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Web.Components.MasterData;
using Shouldly;

namespace ServiceDesk.Web.Tests.Components.MasterData;

// The agent has a HARDWARE competency and one of the deactivated OTHER category; three categories are active.
public sealed class AgentCompetenciesDialogTests : BunitContext
{
    private static readonly ActiveCategoryDto Hardware = new(Guid.CreateVersion7(), "HARDWARE", "Hardver", "Eszközök.");
    private static readonly ActiveCategoryDto Network = new(Guid.CreateVersion7(), "NETWORK", "Hálózat", "Hálózat és VPN.");
    private static readonly ActiveCategoryDto Software = new(Guid.CreateVersion7(), "SOFTWARE", "Szoftver", "Alkalmazások.");

    private static readonly AgentCompetencyDto HardwareCompetency = new(Hardware.Id, "HARDWARE", "Hardver", IsActive: true);
    private static readonly AgentCompetencyDto OtherCompetency = new(Guid.CreateVersion7(), "OTHER", "Egyéb", IsActive: false);

    public AgentCompetenciesDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task Render_Competencies_OffersOnlyActiveCategoriesAndShowsKeptInactiveOne()
    {
        // Arrange
        var cut = RenderProviders();

        // Act
        await ShowAsync(cut, [Hardware, Network, Software], [HardwareCompetency, OtherCompetency]);

        // Assert
        cut.FindComponents<MudListItem<Guid>>().Select(item => item.Instance.Value).ShouldBe([Hardware.Id, Network.Id, Software.Id]);
        cut.FindComponent<MudList<Guid>>().Instance.GetState(x => x.SelectedValues).ShouldBe([Hardware.Id]);
        cut.Find("#kept-competencies").TextContent.ShouldContain("Az inaktív kategóriájú kompetenciák megmaradnak: Egyéb");
        cut.Find("#competencies-submit").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task Submit_SelectionChanged_ReturnsOnlyChosenActiveCategories()
    {
        // Arrange
        var cut = RenderProviders();
        var dialog = await ShowAsync(cut, [Hardware, Network, Software], [HardwareCompetency, OtherCompetency]);

        // Act: Hálózat is added, Hardver is removed.
        await ListItem(cut, Network).Find(".mud-list-item").ClickAsync(new());
        await ListItem(cut, Hardware).Find(".mud-list-item").ClickAsync(new());
        await cut.Find("#competencies-submit").ClickAsync(new());

        // Assert: the inactive OTHER competency is not sent; SetAgentCompetencies keeps it.
        var result = await dialog.Result;
        result.ShouldNotBeNull().Canceled.ShouldBeFalse();
        result.Data.ShouldBeAssignableTo<IReadOnlyList<Guid>>().ShouldBe([Network.Id]);
    }

    [Fact]
    public async Task Render_NoActiveCategoryAndNoInactiveCompetency_ShowsWarningOnly()
    {
        // Arrange
        var cut = RenderProviders();

        // Act
        await ShowAsync(cut, [], []);

        // Assert
        cut.Find("#no-active-categories").TextContent.ShouldContain("Jelenleg nincs aktív kategória.");
        cut.FindComponents<MudList<Guid>>().ShouldBeEmpty();
        cut.FindAll("#kept-competencies").ShouldBeEmpty();
        cut.Find("#competencies-submit").HasAttribute("disabled").ShouldBeTrue();
    }

    // The dialog provider shows the dialog, the popover provider is needed by the MudBlazor inputs.
    private IRenderedComponent<ContainerFragment> RenderProviders() =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
        });

    private Task<IDialogReference> ShowAsync(
        IRenderedComponent<ContainerFragment> cut,
        IReadOnlyList<ActiveCategoryDto> categories,
        IReadOnlyList<AgentCompetencyDto> competencies)
    {
        var parameters = new DialogParameters<AgentCompetenciesDialog>
        {
            { dialog => dialog.Categories, categories },
            { dialog => dialog.Competencies, competencies },
        };
        var dialogService = Services.GetRequiredService<IDialogService>();
        return cut.InvokeAsync(() => dialogService.ShowAsync<AgentCompetenciesDialog>("Kompetenciák", parameters));
    }

    private static IRenderedComponent<MudListItem<Guid>> ListItem(IRenderedComponent<ContainerFragment> cut, ActiveCategoryDto category) =>
        cut.FindComponents<MudListItem<Guid>>().Single(item => item.Instance.Value == category.Id);
}
