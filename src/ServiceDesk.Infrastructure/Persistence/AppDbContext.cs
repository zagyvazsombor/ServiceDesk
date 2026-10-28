using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Persistence;

// Single DbContext for the Identity and domain tables, with one migration history (AGENTS.md §5).
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
}
