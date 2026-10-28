using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Security;
using Shouldly;

namespace ServiceDesk.Application.Tests.Common.Security;

// AGENTS.md §8, level 2: the checks every handler starts with.
public sealed class CurrentUserAuthorizationTests
{
    [Fact]
    public void RequireAuthenticated_AnonymousUser_ReturnsForbidden()
    {
        // Act
        var result = TestUsers.Anonymous().RequireAuthenticated();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthErrors.Forbidden);
    }

    [Fact]
    public void RequireAuthenticated_AuthenticatedWithoutUserId_ReturnsForbidden()
    {
        // Arrange: an inconsistent ICurrentUser must not let a handler run without a user Id.
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns((string?)null);

        // Act
        var result = currentUser.RequireAuthenticated();

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
    }

    [Fact]
    public void RequireAuthenticated_SignedInUser_ReturnsUserId()
    {
        // Act
        var result = TestUsers.Reporter().RequireAuthenticated();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(TestUsers.UserId);
    }

    [Fact]
    public void RequireRole_AnonymousUser_ReturnsForbidden()
    {
        // Act
        var result = TestUsers.Anonymous().RequireRole(Roles.Admin);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
    }

    [Theory]
    [InlineData(Roles.Reporter, Roles.Admin)]
    [InlineData(Roles.Admin, Roles.Reporter)]
    public void RequireRole_SignedInUserWithOtherRole_ReturnsForbidden(string userRole, string requiredRole)
    {
        // Act
        var result = TestUsers.SignedIn(userRole).RequireRole(requiredRole);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
    }

    [Fact]
    public void RequireRole_SignedInUserWithoutRole_ReturnsForbidden()
    {
        // Act
        var result = TestUsers.SignedIn().RequireRole(Roles.Reporter);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
    }

    [Theory]
    [InlineData(Roles.Reporter)]
    [InlineData(Roles.Admin)]
    public void RequireRole_SignedInUserWithRole_ReturnsUserId(string role)
    {
        // Act
        var result = TestUsers.SignedIn(role).RequireRole(role);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(TestUsers.UserId);
    }
}
