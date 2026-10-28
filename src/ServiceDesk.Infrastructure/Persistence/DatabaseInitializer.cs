using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Infrastructure.Persistence.Seed;

namespace ServiceDesk.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    // Development-only startup step (AGENTS.md §7): migrations, then the idempotent seed; EnsureCreated() is forbidden.
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync(ct);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(ct);
    }
}
