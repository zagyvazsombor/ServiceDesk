using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Persistence;

// Single DbContext for the Identity and domain tables, with one migration history (AGENTS.md §5).
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Agent> Agents => Set<Agent>();

    public DbSet<CategorySuggestion> CategorySuggestions => Set<CategorySuggestion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The IEntityTypeConfiguration classes of Persistence/Configurations (AGENTS.md §5).
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
