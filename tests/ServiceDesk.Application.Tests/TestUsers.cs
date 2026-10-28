using NSubstitute;
using ServiceDesk.Application.Common.Security;

namespace ServiceDesk.Application.Tests;

// ICurrentUser substitutes in the states the UseCaseRunner can produce.
internal static class TestUsers
{
    public const string UserId = "user-1";

    public static ICurrentUser Anonymous()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns((string?)null);
        currentUser.IsAuthenticated.Returns(false);
        return currentUser;
    }

    public static ICurrentUser SignedIn(params string[] roles)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(UserId);
        currentUser.IsAuthenticated.Returns(true);
        currentUser.IsInRole(Arg.Any<string>()).Returns(call => roles.Contains(call.Arg<string>()));
        return currentUser;
    }

    public static ICurrentUser Reporter() => SignedIn(Roles.Reporter);

    public static ICurrentUser Admin() => SignedIn(Roles.Admin);
}
