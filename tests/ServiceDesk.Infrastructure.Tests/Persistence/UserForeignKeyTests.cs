using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

// AGENTS.md §7: the user references have DeleteBehavior.Restrict, enforced by the database.
public sealed class UserForeignKeyTests(DatabaseFixture database) : IntegrationTest(database)
{
    // SQL Server: the statement conflicted with a FOREIGN KEY or REFERENCE constraint.
    private const int ForeignKeyViolation = 547;

    [Fact]
    public async Task SaveChangesAsync_DeleteUserWithTicket_ThrowsForeignKeyViolation()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var category = new CategoryBuilder().Build();
        var ticket = new TicketBuilder(reporter, category).Build();
        await SaveAsync(reporter, category, ticket);
        await using var scope = Database.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(candidate => candidate.Id == reporter.Id, CancellationToken);
        dbContext.Users.Remove(user);

        // Act
        var exception = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(CancellationToken));

        // Assert
        exception.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(ForeignKeyViolation);
        await using var readScope = Database.CreateScope();
        var userExists = await readScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.AnyAsync(candidate => candidate.Id == reporter.Id, CancellationToken);
        userExists.ShouldBeTrue();
    }
}
