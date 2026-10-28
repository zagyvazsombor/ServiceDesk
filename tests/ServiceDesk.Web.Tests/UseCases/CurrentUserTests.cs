using System.Security.Claims;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.UseCases;

public sealed class CurrentUserTests
{
    [Fact]
    public void NewInstance_NotInitialized_IsAnonymous()
    {
        // Act
        var currentUser = new CurrentUser();

        // Assert
        currentUser.UserId.ShouldBeNull();
        currentUser.IsAuthenticated.ShouldBeFalse();
        currentUser.IsInRole(Roles.Reporter).ShouldBeFalse();
    }

    [Fact]
    public void Initialize_SignedInReporter_ExposesUserIdAndRole()
    {
        // Arrange
        var currentUser = new CurrentUser();

        // Act
        currentUser.Initialize(TestUsers.Reporter);

        // Assert
        currentUser.UserId.ShouldBe(TestUsers.UserId);
        currentUser.IsAuthenticated.ShouldBeTrue();
        currentUser.IsInRole(Roles.Reporter).ShouldBeTrue();
        currentUser.IsInRole(Roles.Admin).ShouldBeFalse();
    }

    [Fact]
    public void Initialize_UnauthenticatedPrincipalWithClaims_IsAnonymous()
    {
        // Arrange: the claims are there, but the identity has no authentication type.
        var currentUser = new CurrentUser();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, TestUsers.UserId), new Claim(ClaimTypes.Role, Roles.Admin)]));

        // Act
        currentUser.Initialize(principal);

        // Assert
        currentUser.UserId.ShouldBeNull();
        currentUser.IsAuthenticated.ShouldBeFalse();
        currentUser.IsInRole(Roles.Admin).ShouldBeFalse();
    }
}
