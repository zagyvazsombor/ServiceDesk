using System.Security.Claims;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Users.GetCurrentUserProfile;
using ServiceDesk.Domain.Common;
using ServiceDesk.Web.Components.Layout;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Layout;

public sealed class UserMenuTests : BunitContext
{
    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();

    public UserMenuTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Render_Reporter_ShowsEmailOfProfileAndLogoutForm()
    {
        // Arrange
        SignIn(Roles.Reporter);
        ProfileReturns(new CurrentUserProfileDto(TestUsers.UserId, TestUsers.Email, [Roles.Reporter], null));

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
    public void Render_Admin_ShowsDisplayNameOfAgentProfile()
    {
        // Arrange
        SignIn(Roles.Admin);
        ProfileReturns(new CurrentUserProfileDto(
            TestUsers.UserId, TestUsers.Email, [Roles.Admin], new AgentProfileDto("Kiss Anna", IsActive: true)));

        // Act
        var cut = Render<UserMenu>();

        // Assert
        cut.Find("a[href='Account/Manage']").TextContent.Trim().ShouldBe("Kiss Anna");
    }

    [Fact]
    public void Render_ProfileResultFails_ShowsEmailClaim()
    {
        // Arrange
        SignIn(Roles.Admin);
        ProfileReturns(AuthErrors.Forbidden);

        // Act
        var cut = Render<UserMenu>();

        // Assert
        cut.Find("a[href='Account/Manage']").TextContent.Trim().ShouldBe(TestUsers.Email);
    }

    [Fact]
    public async Task Render_Anonymous_RendersNothingAndDoesNotRunUseCase()
    {
        // Arrange
        AddAuthorization();

        // Act
        var cut = Render<UserMenu>();

        // Assert
        cut.Markup.Trim().ShouldBeEmpty();
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<GetCurrentUserProfileHandler, Result<CurrentUserProfileDto>>(
            default!, Xunit.TestContext.Current.CancellationToken);
    }

    private void SignIn(string role)
    {
        var authorization = AddAuthorization();
        authorization.SetAuthorized(TestUsers.Email);
        authorization.SetRoles(role);
        authorization.SetClaims(new Claim(ClaimTypes.Email, TestUsers.Email));
    }

    private void ProfileReturns(Result<CurrentUserProfileDto> result) =>
        _runner.RunAsync(
                Arg.Any<Func<GetCurrentUserProfileHandler, CancellationToken, Task<Result<CurrentUserProfileDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(result);
}
