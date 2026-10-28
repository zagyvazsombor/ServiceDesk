using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Users.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileHandler(ICurrentUser currentUser, IUserQueries userQueries)
{
    public async Task<Result<CurrentUserProfileDto>> HandleAsync(GetCurrentUserProfileQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // AGENTS.md §8, level 2: every signed-in user may read their own profile, whatever their role.
        var authorization = currentUser.RequireAuthenticated();
        if (authorization.IsFailure)
        {
            return authorization.Error;
        }

        // Level 3: the query has no Id of its own, it always reads the profile of the signed-in user.
        var profile = await userQueries.GetProfileAsync(authorization.Value, ct);

        // A user deleted while the cookie is still valid is not a valid user any more.
        if (profile is null)
        {
            return AuthErrors.Forbidden;
        }

        return profile;
    }
}
