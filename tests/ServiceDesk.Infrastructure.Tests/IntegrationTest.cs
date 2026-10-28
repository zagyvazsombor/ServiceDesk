using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Infrastructure.Tests;

// Base class of the LocalDB tests: every test starts from an empty database (AGENTS.md §12).
public abstract class IntegrationTest(DatabaseFixture database) : IAsyncLifetime
{
    protected DatabaseFixture Database { get; } = database;

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await Database.ResetAsync(CancellationToken);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    // Saves the arranged entities through a DbContext of their own, so the test reads them back from the database.
    protected async Task SaveAsync(params object[] entities)
    {
        await using var scope = Database.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync(CancellationToken);
    }
}
