using Microsoft.AspNetCore.Identity;

namespace ServiceDesk.Infrastructure.Identity;

// Identity user of the application; domain entities refer to it only by its string Id (AGENTS.md §5).
public sealed class ApplicationUser : IdentityUser
{
}
