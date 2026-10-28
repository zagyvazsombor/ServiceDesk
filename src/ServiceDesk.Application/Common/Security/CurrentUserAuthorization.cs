using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Security;

// AGENTS.md §5, §8 (level 2): the first step of every handler. On success the value is the Id of the signed-in user,
// so the handler does not have to null-check ICurrentUser.UserId. The UseCaseRunner logs the denials (AGENTS.md §11).
public static class CurrentUserAuthorization
{
    public static Result<string> RequireAuthenticated(this ICurrentUser currentUser)
    {
        ArgumentNullException.ThrowIfNull(currentUser);

        return currentUser is { IsAuthenticated: true, UserId: { } userId }
            ? userId
            : AuthErrors.Forbidden;
    }

    // An anonymous user and a signed-in user without the role get the same error.
    public static Result<string> RequireRole(this ICurrentUser currentUser, string role)
    {
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        var authentication = currentUser.RequireAuthenticated();
        return authentication.IsSuccess && currentUser.IsInRole(role)
            ? authentication
            : AuthErrors.Forbidden;
    }
}
