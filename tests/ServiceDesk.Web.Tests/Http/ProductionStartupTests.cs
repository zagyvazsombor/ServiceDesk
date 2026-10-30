using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Infrastructure.Persistence;
using Shouldly;

namespace ServiceDesk.Web.Tests.Http;

// ADR-009: the real Program.cs in Production, with a real database. The roles are seeded at every start, the demo data
// never; a registered user gets the Reporter role (AGENTS.md §8).
[Collection(nameof(ProductionDatabaseCollection))]
public sealed class ProductionStartupTests(ProductionDatabaseFixture database)
{
    private static readonly string[] DemoEmails =
    [
        "admin.anna@servicedesk.local",
        "admin.bela@servicedesk.local",
        "admin.csaba@servicedesk.local",
        "bejelento.dora@servicedesk.local",
        "bejelento.erik@servicedesk.local",
    ];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_ProductionTwice_CreatesTheRolesOnceWithoutDemoData()
    {
        // Act: two starts on the same database.
        await StartAndStopAsync();
        await StartAndStopAsync();

        // Assert
        await using var scope = database.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleNames = await dbContext.Roles.Select(role => role.Name).ToListAsync(CancellationToken);
        roleNames.ShouldBe([Roles.Admin, Roles.Reporter], ignoreOrder: true);
        (await dbContext.Users.AnyAsync(user => DemoEmails.Contains(user.Email), CancellationToken)).ShouldBeFalse();
        (await dbContext.Categories.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.Agents.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task PostRegister_Production_CreatesUserWithTheReporterRoleOnly()
    {
        // Arrange
        await using var factory = new ProductionWebApplicationFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var email = AccountForms.NewEmail();

        // Act
        using var response = await AccountForms.RegisterAsync(client, email, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        await using var scope = database.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleNames = await (
                from user in dbContext.Users
                where user.Email == email
                join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                select role.Name)
            .ToListAsync(CancellationToken);
        roleNames.ShouldBe([Roles.Reporter]);
    }

    private static async Task StartAndStopAsync()
    {
        await using var factory = new ProductionWebApplicationFactory();
        using var client = factory.CreateClient(); // starts the host, so the startup steps of Program.cs run
    }
}
