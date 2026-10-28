using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Tests.Builders;

// An Identity user row without password or role; the domain tables only refer to its Id.
internal sealed class UserBuilder
{
    private readonly string _email = $"user-{Guid.NewGuid():N}@servicedesk.test";

    public ApplicationUser Build() => new()
    {
        UserName = _email,
        NormalizedUserName = _email.ToUpperInvariant(),
        Email = _email,
        NormalizedEmail = _email.ToUpperInvariant(),
    };
}
