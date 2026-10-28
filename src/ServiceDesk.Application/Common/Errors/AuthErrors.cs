using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Errors;

public static class AuthErrors
{
    // AGENTS.md §8: returned both for an anonymous user and for a wrong role.
    public static readonly Error Forbidden = new(
        "Auth.Forbidden",
        "Nincs jogosultságod ehhez a művelethez.",
        ErrorType.Forbidden);
}
