using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using ServiceDesk.Application.Tickets.GetAssignableAgents;
using ServiceDesk.Web.Components.Tickets;
using Shouldly;

namespace ServiceDesk.Web.Tests.Components.Tickets;

public sealed class AssignTicketDialogTests : BunitContext
{
    private static readonly AssignableAgentDto Csaba =
        new(Guid.CreateVersion7(), "Tóth Csaba", Load: 0, OpenTicketCount: 0, IsCurrentAssignee: false);

    private static readonly AssignableAgentDto Anna =
        new(Guid.CreateVersion7(), "Kiss Anna", Load: 3, OpenTicketCount: 1, IsCurrentAssignee: true);

    public AssignTicketDialogTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task Render_Candidates_ListsLoadAndDisablesCurrentAssignee()
    {
        // Arrange
        var cut = RenderProviders();

        // Act
        await ShowAsync(cut, [Csaba, Anna]);
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<Guid?>>().Instance.OpenMenu());

        // Assert
        // MudSelect renders its items twice: once to register them, once in the open list.
        var items = cut.FindComponents<MudSelectItem<Guid?>>()
            .Where(item => item.Markup.Contains("terhelés", StringComparison.Ordinal))
            .ToList();
        items.Select(item => item.Instance.Value).ShouldBe([Csaba.AgentId, Anna.AgentId]);
        items[0].Markup.ShouldContain("Tóth Csaba – terhelés: 0, nyitott jegy: 0");
        items[0].Instance.Disabled.ShouldBeFalse();
        items[1].Markup.ShouldContain("Kiss Anna – terhelés: 3, nyitott jegy: 1 (jelenlegi felelős)");
        items[1].Instance.Disabled.ShouldBeTrue();
        cut.Find("#assign-submit").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task Submit_AgentSelected_ClosesWithAgentId()
    {
        // Arrange
        var cut = RenderProviders();
        var dialog = await ShowAsync(cut, [Csaba, Anna]);

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<MudSelect<Guid?>>().Instance.SelectOption(Csaba.AgentId));
        await cut.Find("#assign-submit").ClickAsync(new());

        // Assert
        var result = await dialog.Result;
        result.ShouldNotBeNull();
        result.Canceled.ShouldBeFalse();
        result.Data.ShouldBe(Csaba.AgentId);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task Render_NoCandidates_ShowsWarningAndDisablesSubmit()
    {
        // Arrange
        var cut = RenderProviders();

        // Act
        await ShowAsync(cut, []);

        // Assert
        cut.Find("#no-assignable-agents").TextContent.ShouldContain("Nincs aktív ügyintéző");
        cut.FindComponents<MudSelect<Guid?>>().ShouldBeEmpty();
        cut.Find("#assign-submit").HasAttribute("disabled").ShouldBeTrue();
    }

    // The dialog provider shows the dialog, the popover provider the items of an open select.
    private IRenderedComponent<ContainerFragment> RenderProviders() =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
        });

    private Task<IDialogReference> ShowAsync(IRenderedComponent<ContainerFragment> cut, IReadOnlyList<AssignableAgentDto> agents)
    {
        var parameters = new DialogParameters<AssignTicketDialog> { { dialog => dialog.Agents, agents } };
        var dialogService = Services.GetRequiredService<IDialogService>();
        return cut.InvokeAsync(() => dialogService.ShowAsync<AssignTicketDialog>("Kiosztás", parameters));
    }
}

