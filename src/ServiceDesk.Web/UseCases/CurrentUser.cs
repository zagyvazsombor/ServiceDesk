using System.Security.Claims;
using ServiceDesk.Application.Common.Security;

namespace ServiceDesk.Web.UseCases;

// AGENTS.md §5, §8: the user of one use case scope. The UseCaseRunner initializes it from the circuit
// authentication state; until then it is anonymous.
public sealed class CurrentUser : ICurrentUser
{
    private ClaimsPrincipal _principal = new(new ClaimsIdentity());

    public string? UserId { get; private set; }

    public bool IsAuthenticated => UserId is not null;

    public bool IsInRole(string role) => IsAuthenticated && _principal.IsInRole(role);

    public void Initialize(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        _principal = principal;
        // The Identity user Id claim; an unauthenticated principal has no user.
        UserId = principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
    }
}
