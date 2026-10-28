using System.Security.Claims;
using Bunit;
using MudBlazor.Services;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Web.Components.Layout;
using Shouldly;

namespace ServiceDesk.Web.Tests.Layout;

public sealed class UserMenuTests : BunitContext
{
    public UserMenuTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Render_SignedInUser_ShowsEmailAndLogoutForm()
    {
        // Arrange
        var authorization = AddAuthorization();
        authorization.SetAuthorized(TestUsers.Email);
        authorization.SetRoles(Roles.Reporter);
        authorization.SetClaims(new Claim(ClaimTypes.Email, TestUsers.Email));

        // Act
        var cut = Render<UserMenu>();

        // Assert
        cut.Find("a[href='Account/Manage']").TextContent.Trim().ShouldBe(TestUsers.Email);
        var form = cut.Find("form");
        form.GetAttribute("action").ShouldBe("Account/Logout");
        form.GetAttribute("method").ShouldBe("post");
        form.QuerySelector("input[name='ReturnUrl']").ShouldNotBeNull();
        form.QuerySelector("button[type='submit']").ShouldNotBeNull().TextContent.Trim().ShouldBe("Kijelentkezés");
    }

    [Fact]
    public void Render_Anonymous_RendersNothing()
    {
        // Arrange
        AddAuthorization();

        // Act
        var cut = Render<UserMenu>();

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
    }
}
