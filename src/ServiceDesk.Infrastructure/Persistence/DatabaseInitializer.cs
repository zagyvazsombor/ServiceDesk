using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceDesk.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    // Development-only startup step (AGENTS.md §7); EnsureCreated() is forbidden.
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync(ct);
    }
}
