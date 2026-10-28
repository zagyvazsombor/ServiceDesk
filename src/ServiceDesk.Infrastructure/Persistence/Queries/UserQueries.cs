using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Users.GetCurrentUserProfile;

namespace ServiceDesk.Infrastructure.Persistence.Queries;

internal sealed class UserQueries(AppDbContext dbContext) : IUserQueries
{
    // One query: the e-mail of the user, its roles and its agent profile, projected straight into the DTO.
    public Task<CurrentUserProfileDto?> GetProfileAsync(string userId, CancellationToken ct) =>
        dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new CurrentUserProfileDto(
                user.Id,
                user.Email,
                dbContext.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(dbContext.Roles, userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
                    .OrderBy(roleName => roleName)
                    .ToList(),
                // At most one, because Agent.UserId is unique (AGENTS.md §7, §10).
                dbContext.Agents
                    .Where(agent => agent.UserId == user.Id)
                    .Select(agent => new AgentProfileDto(agent.DisplayName, agent.IsActive))
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(ct);
}
