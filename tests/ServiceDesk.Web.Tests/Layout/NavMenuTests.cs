using Bunit;
using MudBlazor.Services;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Web.Components.Layout;
using Shouldly;

namespace ServiceDesk.Web.Tests.Layout;

// AGENTS.md §8: the menu shows the links of the signed-in user's role only (UI level).
public sealed class NavMenuTests : BunitContext
{
    private static readonly string[] ReporterLinks = ["tickets/new", "tickets/my"];
    private static readonly string[] AdminLinks = ["admin/queue", "admin/categories", "admin/agents"];
    private static readonly string[] AccountLinks = ["Account/Register", "Account/Login"];

    public NavMenuTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Render_Reporter_ShowsReporterLinksOnly()
    {
        // Arrange
        var authorization = AddAuthorization();
        authorization.SetAuthorized(TestUsers.Email);
        authorization.SetRoles(Roles.Reporter);

        // Act
        var links = RenderedLinks();

        // Assert
        ReporterLinks.ShouldBeSubsetOf(links);
        links.ShouldNotContain(link => AdminLinks.Contains(link) || AccountLinks.Contains(link));
    }

    [Fact]
    public void Render_Admin_ShowsAdminLinksOnly()
    {
        // Arrange
        var authorization = AddAuthorization();
        authorization.SetAuthorized(TestUsers.Email);
        authorization.SetRoles(Roles.Admin);

        // Act
        var links = RenderedLinks();

        // Assert
        AdminLinks.ShouldBeSubsetOf(links);
        links.ShouldNotContain(link => ReporterLinks.Contains(link) || AccountLinks.Contains(link));
    }

    [Fact]
    public void Render_Anonymous_ShowsRegisterAndLoginOnly()
    {
        // Arrange
        AddAuthorization();

        // Act
        var links = RenderedLinks();

        // Assert
        AccountLinks.ShouldBeSubsetOf(links);
        links.ShouldNotContain(link => ReporterLinks.Contains(link) || AdminLinks.Contains(link));
    }

    private List<string> RenderedLinks() =>
        Render<NavMenu>().FindAll("a").Select(link => link.GetAttribute("href") ?? string.Empty).ToList();
}
