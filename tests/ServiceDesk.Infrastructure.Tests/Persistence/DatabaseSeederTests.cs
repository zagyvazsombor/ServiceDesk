using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Infrastructure.Persistence;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

// The seeders are internal; they run through InitializeDatabaseAsync and SeedRolesAsync, the entry points of the
// Development and of every other startup.
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

    [Fact]
    public async Task SeedRolesAsync_RunTwice_CreatesOnlyTheTwoRolesOnce()
    {
        // Act: the startup step of every environment other than Development (ADR-009).
        await Database.Services.SeedRolesAsync(CancellationToken);
        await Database.Services.SeedRolesAsync(CancellationToken);

        // Assert: no demo data.
        (await CountRecordsAsync()).ShouldBe(new RecordCounts(Roles: 2, Users: 0, UserRoles: 0, Categories: 0, Agents: 0, Competencies: 0));
        await using var scope = Database.CreateScope();
        var roleNames = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Roles
            .Select(role => role.Name)
            .ToListAsync(CancellationToken);
        roleNames.ShouldBe([Roles.Admin, Roles.Reporter], ignoreOrder: true);
    }

    [Fact]
    public async Task InitializeDatabaseAsync_RolesSeededBefore_AddsOnlyTheDemoData()
    {
        // Arrange: the database of an environment where the roles were seeded earlier.
        await Database.Services.SeedRolesAsync(CancellationToken);

        // Act
        await Database.Services.InitializeDatabaseAsync(CancellationToken);

        // Assert
        (await CountRecordsAsync()).ShouldBe(new RecordCounts(Roles: 2, Users: 5, UserRoles: 5, Categories: 5, Agents: 3, Competencies: 7));
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
