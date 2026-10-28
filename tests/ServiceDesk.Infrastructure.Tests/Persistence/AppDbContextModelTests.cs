using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Infrastructure.Persistence;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

// AGENTS.md §15: a model change must come with a migration. The check compares the model with the
// migration snapshot and does not open a database connection.
public sealed class AppDbContextModelTests
{
    [Fact]
    public void HasPendingModelChanges_AfterLatestMigration_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = TestDatabase.ConnectionString })
            .Build();
        // Resolved through AddInfrastructure: IdentityDbContext reads the schema version (passkeys table) from the
        // registered IdentityOptions, so a context created with `new` would build a different model.
        using var serviceProvider = new ServiceCollection()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hasPendingModelChanges = dbContext.Database.HasPendingModelChanges();

        hasPendingModelChanges.ShouldBeFalse();
    }
}
