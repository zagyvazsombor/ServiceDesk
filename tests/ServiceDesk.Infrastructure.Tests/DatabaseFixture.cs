using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Tests;

[assembly: AssemblyFixture(typeof(DatabaseFixture))]

namespace ServiceDesk.Infrastructure.Tests;

// AGENTS.md §12: migrates the ServiceDesk_Tests database once per test run; ResetAsync empties every table except the
// migration history, so each test starts from an empty, up-to-date schema.
public sealed class DatabaseFixture : IAsyncLifetime
{
    // Test value only; the seeder needs a password that satisfies the Identity password rules.
    private const string SeedPassword = "Teszt-Jelszo-2026";

    private readonly ServiceProvider _services;
    private Respawner? _respawner;

    public DatabaseFixture()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestDatabase.ConnectionString,
                ["Seed:DefaultPassword"] = SeedPassword,
            })
            .Build();

        // The same registrations as the application; AddLogging stands in for the logging of the Web host.
        _services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
    }

    public IServiceProvider Services => _services;

    public AsyncServiceScope CreateScope() => _services.CreateAsyncScope();

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var scope = CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync(ct);
        }

        await using var connection = new SqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync(ct);
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = [HistoryRepository.DefaultTableName],
        });
    }

    public async Task ResetAsync(CancellationToken ct)
    {
        var respawner = _respawner ?? throw new InvalidOperationException("The database fixture is not initialized.");

        await using var connection = new SqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync(ct);
        await respawner.ResetAsync(connection);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
