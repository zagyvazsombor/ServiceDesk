using ServiceDesk.Application.Users.GetCurrentUserProfile;

namespace ServiceDesk.Application.Common.Persistence;

// Read side of the Identity users: a projection straight into the DTO, without tracking (AGENTS.md §7).
public interface IUserQueries
{
    // null if the user does not exist.
    Task<CurrentUserProfileDto?> GetProfileAsync(string userId, CancellationToken ct);
}
