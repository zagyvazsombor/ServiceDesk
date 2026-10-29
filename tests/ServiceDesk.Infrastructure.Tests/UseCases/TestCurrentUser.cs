using ServiceDesk.Application.Common.Security;

namespace ServiceDesk.Infrastructure.Tests.UseCases;

// A signed-in user, as the UseCaseRunner sets up ICurrentUser from the circuit (AGENTS.md §5).
internal sealed class TestCurrentUser(string userId, params string[] roles) : ICurrentUser
{
    public string? UserId => userId;

    public bool IsAuthenticated => true;

    public bool IsInRole(string role) => roles.Contains(role, StringComparer.Ordinal);
}
