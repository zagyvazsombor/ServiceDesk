using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Persistence.Seed;

// AGENTS.md §7: development demo data. Idempotent: it only creates what is missing and never overwrites existing data.
internal sealed class DatabaseSeeder(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<SeedOptions> options,
    ILogger<DatabaseSeeder> logger)
{
    private static readonly string[] SeedRoles = [Roles.Reporter, Roles.Admin];

    private static readonly SeedCategory[] SeedCategories =
    [
        new("HARDWARE", "Hardver", "Számítógépek, monitorok, nyomtatók és egyéb eszközök hibái."),
        new("SOFTWARE", "Szoftver", "Alkalmazások telepítése, működési hibái és frissítései."),
        new("NETWORK", "Hálózat", "Internet- és hálózati kapcsolat, Wi-Fi és VPN problémái."),
        new("ACCESS", "Jogosultság és hozzáférés", "Fiókok, jelszavak, jogosultságok és hozzáférési kérelmek."),
        new("OTHER", "Egyéb", "Minden olyan kérés, amely nem illik a többi kategóriába."),
    ];

    private static readonly SeedUser[] SeedUsers =
    [
        new("admin.anna@servicedesk.local", Roles.Admin, new SeedAgent("Kiss Anna", ["HARDWARE", "NETWORK"])),
        new("admin.bela@servicedesk.local", Roles.Admin, new SeedAgent("Nagy Béla", ["SOFTWARE", "ACCESS"])),
        new("admin.csaba@servicedesk.local", Roles.Admin, new SeedAgent("Tóth Csaba", ["NETWORK", "SOFTWARE", "OTHER"])),
        new("bejelento.dora@servicedesk.local", Roles.Reporter, null),
        new("bejelento.erik@servicedesk.local", Roles.Reporter, null),
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        var password = options.Value.DefaultPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"Configuration value '{SeedOptions.SectionName}:{nameof(SeedOptions.DefaultPassword)}' is missing.");
        }

        var createdRoles = await SeedRolesAsync();
        var (userIds, createdUsers) = await SeedUsersAsync(password);
        var (categoryIds, createdCategories) = await SeedCategoriesAsync(ct);
        var createdAgents = await SeedAgentsAsync(userIds, categoryIds, ct);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Database seed finished; created {RoleCount} roles, {UserCount} users, {CategoryCount} categories, {AgentCount} agents.",
            createdRoles,
            createdUsers,
            createdCategories,
            createdAgents);
    }

    private async Task<int> SeedRolesAsync()
    {
        var created = 0;
        foreach (var role in SeedRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)));
                created++;
            }
        }

        return created;
    }

    // The UserManager saves each user immediately; returns the user Id of every seed e-mail address.
    private async Task<(Dictionary<string, string> UserIds, int Created)> SeedUsersAsync(string password)
    {
        var userIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var created = 0;
        foreach (var seedUser in SeedUsers)
        {
            var user = await userManager.FindByEmailAsync(seedUser.Email);
            if (user is null)
            {
                // EmailConfirmed: there is no e-mail infrastructure, and the sign-in requires a confirmed account.
                user = new ApplicationUser { UserName = seedUser.Email, Email = seedUser.Email, EmailConfirmed = true };
                EnsureSucceeded(await userManager.CreateAsync(user, password));
                created++;
            }

            if (!await userManager.IsInRoleAsync(user, seedUser.Role))
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(user, seedUser.Role));
            }

            userIds[seedUser.Email] = user.Id;
        }

        return (userIds, created);
    }

    // Returns the Id of every category code, including the ones added now but not saved yet.
    private async Task<(Dictionary<string, Guid> CategoryIds, int Created)> SeedCategoriesAsync(CancellationToken ct)
    {
        var categoryIds = await dbContext.Categories.ToDictionaryAsync(category => category.Code, category => category.Id, ct);
        var created = 0;
        foreach (var seedCategory in SeedCategories.Where(seedCategory => !categoryIds.ContainsKey(seedCategory.Code)))
        {
            var category = EnsureSucceeded(Category.Create(seedCategory.Code, seedCategory.Name, seedCategory.Description));
            dbContext.Categories.Add(category);
            categoryIds.Add(category.Code, category.Id);
            created++;
        }

        return (categoryIds, created);
    }

    // Only users without an agent profile get one; existing profiles and competencies are left as they are.
    private async Task<int> SeedAgentsAsync(
        Dictionary<string, string> userIds,
        Dictionary<string, Guid> categoryIds,
        CancellationToken ct)
    {
        var agentUserIds = (await dbContext.Agents.Select(agent => agent.UserId).ToListAsync(ct)).ToHashSet();
        var created = 0;
        foreach (var seedUser in SeedUsers)
        {
            var userId = userIds[seedUser.Email];
            if (seedUser.Agent is null || agentUserIds.Contains(userId))
            {
                continue;
            }

            var agent = EnsureSucceeded(Agent.Create(userId, seedUser.Agent.DisplayName));
            EnsureSucceeded(agent.SetCompetencies(seedUser.Agent.CompetencyCodes.Select(code => categoryIds[code])));
            dbContext.Agents.Add(agent);
            created++;
        }

        return created;
    }

    // Invalid seed data is a programming error.
    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seeding failed: {string.Join(", ", result.Errors.Select(error => error.Code))}");
        }
    }

    private static void EnsureSucceeded(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Seeding failed: {result.Error.Code}");
        }
    }

    private static T EnsureSucceeded<T>(Result<T> result)
    {
        EnsureSucceeded((Result)result);
        return result.Value;
    }

    private sealed record SeedCategory(string Code, string Name, string Description);

    private sealed record SeedUser(string Email, string Role, SeedAgent? Agent);

    private sealed record SeedAgent(string DisplayName, string[] CompetencyCodes);
}
