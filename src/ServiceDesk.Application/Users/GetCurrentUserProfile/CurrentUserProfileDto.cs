namespace ServiceDesk.Application.Users.GetCurrentUserProfile;

// Roles: the role names of the user in alphabetical order. Agent: only Admin users have one (AGENTS.md §10).
public sealed record CurrentUserProfileDto(string UserId, string? Email, IReadOnlyList<string> Roles, AgentProfileDto? Agent);
