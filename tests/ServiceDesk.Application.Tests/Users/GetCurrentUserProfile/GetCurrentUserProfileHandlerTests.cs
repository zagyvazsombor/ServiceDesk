using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Users.GetCurrentUserProfile;
using Shouldly;

namespace ServiceDesk.Application.Tests.Users.GetCurrentUserProfile;

// The query has no Id and any role may read its own profile, so there is no "wrong role" and no "foreign resource"
// case (AGENTS.md §12); the profile is always read for the Id of the signed-in user.
public sealed class GetCurrentUserProfileHandlerTests
{
    private readonly IUserQueries _userQueries = Substitute.For<IUserQueries>();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Arrange
        var handler = new GetCurrentUserProfileHandler(TestUsers.Anonymous(), _userQueries);

        // Act
        var result = await handler.HandleAsync(new GetCurrentUserProfileQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _userQueries.DidNotReceiveWithAnyArgs().GetProfileAsync(default!, CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsOwnProfileWithoutAgent()
    {
        // Arrange
        var profile = new CurrentUserProfileDto(TestUsers.UserId, "bejelento.dora@servicedesk.local", [Roles.Reporter], null);
        ProfileOfSignedInUserIs(profile);
        var handler = new GetCurrentUserProfileHandler(TestUsers.Reporter(), _userQueries);

        // Act
        var result = await handler.HandleAsync(new GetCurrentUserProfileQuery(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(profile);
        result.Value.Agent.ShouldBeNull();
        await _userQueries.Received(1).GetProfileAsync(TestUsers.UserId, CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_Admin_ReturnsOwnProfileWithAgent()
    {
        // Arrange
        var profile = new CurrentUserProfileDto(
            TestUsers.UserId, "admin.anna@servicedesk.local", [Roles.Admin], new AgentProfileDto("Kiss Anna", IsActive: true));
        ProfileOfSignedInUserIs(profile);
        var handler = new GetCurrentUserProfileHandler(TestUsers.Admin(), _userQueries);

        // Act
        var result = await handler.HandleAsync(new GetCurrentUserProfileQuery(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(profile);
        result.Value.Agent.ShouldNotBeNull().DisplayName.ShouldBe("Kiss Anna");
        await _userQueries.Received(1).GetProfileAsync(TestUsers.UserId, CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_SignedInUserWithoutRole_ReturnsOwnProfile()
    {
        // Arrange
        var profile = new CurrentUserProfileDto(TestUsers.UserId, "teszt@servicedesk.local", [], null);
        ProfileOfSignedInUserIs(profile);
        var handler = new GetCurrentUserProfileHandler(TestUsers.SignedIn(), _userQueries);

        // Act
        var result = await handler.HandleAsync(new GetCurrentUserProfileQuery(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(profile);
    }

    [Fact]
    public async Task HandleAsync_UserNoLongerExists_ReturnsForbidden()
    {
        // Arrange
        ProfileOfSignedInUserIs(null);
        var handler = new GetCurrentUserProfileHandler(TestUsers.Reporter(), _userQueries);

        // Act
        var result = await handler.HandleAsync(new GetCurrentUserProfileQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
    }

    private void ProfileOfSignedInUserIs(CurrentUserProfileDto? profile) =>
        _userQueries.GetProfileAsync(TestUsers.UserId, Arg.Any<CancellationToken>()).Returns(profile);
}
