using Microsoft.AspNetCore.Identity;
using ServiceDesk.Application.Common.Security;

namespace ServiceDesk.Infrastructure.Persistence.Seed;

// ADR-009: the Reporter and Admin roles exist in every environment; the registration gives Reporter (AGENTS.md §8).
// Idempotent: it only creates a missing role. Not demo data, so it runs outside Development too.
internal sealed class RoleSeeder(RoleManager<IdentityRole> roleManager)
{
    private static readonly string[] SeedRoles = [Roles.Reporter, Roles.Admin];

    // Returns the number of roles created now.
    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var created = 0;
        foreach (var role in SeedRoles)
        {
            ct.ThrowIfCancellationRequested(); // the RoleManager methods take no token
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Seeding role {role} failed: {string.Join(", ", result.Errors.Select(error => error.Code))}");
            }

            created++;
        }

        return created;
    }
}
