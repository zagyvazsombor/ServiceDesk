using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Users.GetCurrentUserProfile;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

public sealed class UserQueriesTests(DatabaseFixture database) : IntegrationTest(database)
{
    private readonly IdentityRole _reporterRole = new(Roles.Reporter) { NormalizedName = Roles.Reporter.ToUpperInvariant() };
    private readonly IdentityRole _adminRole = new(Roles.Admin) { NormalizedName = Roles.Admin.ToUpperInvariant() };

    [Fact]
    public async Task GetProfileAsync_Reporter_ReturnsEmailAndRoleWithoutAgent()
    {
        // Arrange: an Admin with an agent profile is also in the database, its data must not leak in.
        var reporter = new UserBuilder().Build();
        var admin = new UserBuilder().Build();
        var agent = new AgentBuilder(admin).Build();
        await SaveAsync(_reporterRole, _adminRole, reporter, admin, InRole(reporter, _reporterRole), InRole(admin, _adminRole), agent);

        // Act
        var profile = await GetProfileAsync(reporter.Id);

        // Assert
        profile.ShouldNotBeNull();
        profile.UserId.ShouldBe(reporter.Id);
        profile.Email.ShouldBe(reporter.Email);
        profile.Roles.ShouldBe([Roles.Reporter]);
        profile.Agent.ShouldBeNull();
    }

    [Fact]
    public async Task GetProfileAsync_AdminWithActiveAgent_ReturnsAgentProfile()
    {
        // Arrange
        var admin = new UserBuilder().Build();
        var agent = new AgentBuilder(admin).Build();
        await SaveAsync(_adminRole, admin, InRole(admin, _adminRole), agent);

        // Act
        var profile = await GetProfileAsync(admin.Id);

        // Assert
        profile.ShouldNotBeNull();
        profile.Email.ShouldBe(admin.Email);
        profile.Roles.ShouldBe([Roles.Admin]);
        profile.Agent.ShouldBe(new AgentProfileDto("Kiss Anna", IsActive: true));
    }

    [Fact]
    public async Task GetProfileAsync_AdminWithInactiveAgent_ReturnsInactiveAgentProfile()
    {
        // Arrange
        var admin = new UserBuilder().Build();
        var agent = new AgentBuilder(admin).Inactive().Build();
        await SaveAsync(_adminRole, admin, InRole(admin, _adminRole), agent);

        // Act
        var profile = await GetProfileAsync(admin.Id);

        // Assert
        profile.ShouldNotBeNull().Agent.ShouldBe(new AgentProfileDto("Kiss Anna", IsActive: false));
    }

    [Fact]
    public async Task GetProfileAsync_UnknownUser_ReturnsNull()
    {
        // Arrange
        var user = new UserBuilder().Build();
        await SaveAsync(user);

        // Act
        var profile = await GetProfileAsync("unknown-user");

        // Assert
        profile.ShouldBeNull();
    }

    private static IdentityUserRole<string> InRole(ApplicationUser user, IdentityRole role) =>
        new() { UserId = user.Id, RoleId = role.Id };

    private async Task<CurrentUserProfileDto?> GetProfileAsync(string userId)
    {
        await using var scope = Database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IUserQueries>().GetProfileAsync(userId, CancellationToken);
    }
}
