using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Infrastructure.Persistence;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

// The seeder is internal; it runs through InitializeDatabaseAsync, the same entry point as the Development startup.
public sealed class DatabaseSeederTests(DatabaseFixture database) : IntegrationTest(database)
{
    [Fact]
    public async Task InitializeDatabaseAsync_RunTwice_DoesNotChangeRecordCounts()
    {
        // Arrange
        await Database.Services.InitializeDatabaseAsync(CancellationToken);
        var countsAfterFirstRun = await CountRecordsAsync();

        // Act
        await Database.Services.InitializeDatabaseAsync(CancellationToken);
        var countsAfterSecondRun = await CountRecordsAsync();

        // Assert: the seed table of AGENTS.md §7.
        countsAfterFirstRun.ShouldBe(new RecordCounts(Roles: 2, Users: 5, UserRoles: 5, Categories: 5, Agents: 3, Competencies: 7));
        countsAfterSecondRun.ShouldBe(countsAfterFirstRun);
    }

    private async Task<RecordCounts> CountRecordsAsync()
    {
        await using var scope = Database.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return new RecordCounts(
            await dbContext.Roles.CountAsync(CancellationToken),
            await dbContext.Users.CountAsync(CancellationToken),
            await dbContext.UserRoles.CountAsync(CancellationToken),
            await dbContext.Categories.CountAsync(CancellationToken),
            await dbContext.Agents.CountAsync(CancellationToken),
            await dbContext.Set<AgentCompetency>().CountAsync(CancellationToken));
    }

    private sealed record RecordCounts(int Roles, int Users, int UserRoles, int Categories, int Agents, int Competencies);
}
