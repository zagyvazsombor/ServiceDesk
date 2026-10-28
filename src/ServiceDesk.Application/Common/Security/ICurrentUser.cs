namespace ServiceDesk.Application.Common.Security;

// AGENTS.md §8: the user of the running use case, the input of the handler authorization (level 2).
// The Web layer fills it from the circuit authentication state, never from the HTTP context.
public interface ICurrentUser
{
    // The Identity user Id; null when nobody is signed in.
    string? UserId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
