using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Users.GetCurrentUserProfile;
using ServiceDesk.Domain.Common;
using ServiceDesk.Web.Components.Layout;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Layout;

// The user menu has an ErrorBoundary of its own: its exception does not reach the ErrorBoundary of the page.
public sealed class MainLayoutTests : BunitContext
{
    private const string PageContent = "<p id=\"page-content\">Oldaltartalom</p>";

    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();

    public MainLayoutTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        // The MudStaticNavDrawerToggle of the AppBar reads the persisted state of the drawer.
        this.AddBunitPersistentComponentState();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Render_UserMenuUseCaseThrows_ShowsEmailClaimAndKeepsPage()
    {
        // Arrange
        var authorization = AddAuthorization();
        authorization.SetAuthorized(TestUsers.Email);
        authorization.SetRoles(Roles.Admin);
        authorization.SetClaims(new Claim(ClaimTypes.Email, TestUsers.Email));
        _runner.RunAsync(
                Arg.Any<Func<GetCurrentUserProfileHandler, CancellationToken, Task<Result<CurrentUserProfileDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Result<CurrentUserProfileDto>>(new InvalidOperationException("Test failure.")));

        // Act
        var cut = RenderLayout();

        // Assert: the AppBar shows the e-mail claim instead of the user menu.
        var appBar = cut.Find("header");
        appBar.TextContent.ShouldContain(TestUsers.Email);
        appBar.QuerySelector("a[href='Account/Manage']").ShouldBeNull();
        // The page itself is rendered, without the error message of the page ErrorBoundary.
        cut.Find("#page-content").TextContent.ShouldBe("Oldaltartalom");
        cut.FindAll(".mud-alert").ShouldBeEmpty();
    }

    [Fact]
    public void Render_UserMenuSucceeds_ShowsUserMenuAndPage()
    {
        // Arrange
        var authorization = AddAuthorization();
        authorization.SetAuthorized(TestUsers.Email);
        authorization.SetRoles(Roles.Admin);
        authorization.SetClaims(new Claim(ClaimTypes.Email, TestUsers.Email));
        _runner.RunAsync(
                Arg.Any<Func<GetCurrentUserProfileHandler, CancellationToken, Task<Result<CurrentUserProfileDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CurrentUserProfileDto(
                TestUsers.UserId, TestUsers.Email, [Roles.Admin], new AgentProfileDto("Kiss Anna", IsActive: true))));

        // Act
        var cut = RenderLayout();

        // Assert
        cut.Find("header a[href='Account/Manage']").TextContent.Trim().ShouldBe("Kiss Anna");
        cut.Find("#page-content").TextContent.ShouldBe("Oldaltartalom");
    }

    private IRenderedComponent<MainLayout> RenderLayout()
    {
        // The toggle also reads the render mode; the application runs in interactive Server mode. Set after the
        // services, because the renderer builds the service provider.
        Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        return Render<MainLayout>(parameters => parameters
            .Add(layout => layout.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, PageContent))));
    }
}
