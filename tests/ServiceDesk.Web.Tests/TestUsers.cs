using System.Security.Claims;
using ServiceDesk.Application.Common.Security;

namespace ServiceDesk.Web.Tests;

// Principals in the shape of the Identity cookie: user Id, name, e-mail and role claims.
internal static class TestUsers
{
    public const string UserId = "user-1";

    public const string Email = "bejelento.dora@servicedesk.local";

    public static ClaimsPrincipal SignedIn(string role) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim(ClaimTypes.Name, Email),
            new Claim(ClaimTypes.Email, Email),
            new Claim(ClaimTypes.Role, role),
        ],
        authenticationType: "Test"));

    public static ClaimsPrincipal Reporter => SignedIn(Roles.Reporter);

    public static ClaimsPrincipal Admin => SignedIn(Roles.Admin);

    public static ClaimsPrincipal Anonymous => new(new ClaimsIdentity());
}
