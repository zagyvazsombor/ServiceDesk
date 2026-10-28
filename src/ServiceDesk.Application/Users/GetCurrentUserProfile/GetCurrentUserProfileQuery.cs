namespace ServiceDesk.Application.Users.GetCurrentUserProfile;

// No input: the profile always belongs to the signed-in user (ICurrentUser).
public sealed record GetCurrentUserProfileQuery;
