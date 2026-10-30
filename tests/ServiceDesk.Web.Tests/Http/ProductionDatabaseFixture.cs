using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Persistence;

namespace ServiceDesk.Web.Tests.Http;

// ADR-009: an empty, migrated LocalDB database for the tests of the real Production startup. It is not ServiceDesk_Tests:
// the Infrastructure tests reset that one with Respawn while the test projects run in parallel. Recreated once per test
// run (ProductionDatabaseCollection); Connect Timeout: the first start of a stopped LocalDB instance can take longer
// than the default 15 seconds.
public sealed class ProductionDatabaseFixture : IAsyncLifetime
{
    public const string ConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=ServiceDesk_WebTests;Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=60";

    // The registrations of the application: the Identity options (schema version) are part of the model.
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = ConnectionString })
            .Build())
        .BuildServiceProvider();

    public AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync(ct);
        await dbContext.Database.MigrateAsync(ct);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
