using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Infrastructure.Persistence.Repositories;

internal sealed class AgentRepository(AppDbContext dbContext) : IAgentRepository
{
    public Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Agents
            .Include(agent => agent.Competencies)
            .SingleOrDefaultAsync(agent => agent.Id == id, ct);

    public async Task<IReadOnlyList<AgentCandidate>> GetEligibleCandidatesAsync(Guid categoryId, CancellationToken ct)
    {
        // One query; the load itself is calculated by the domain (AgentCandidate.Load).
        var rows = await dbContext.Agents
            .AsNoTracking()
            .Where(agent => agent.IsActive
                && agent.Competencies.Any(competency => competency.CategoryId == categoryId)) // BR-04
            .Select(agent => new
            {
                agent.Id,
                agent.LastAssignedAt,
                OpenTicketPriorities = dbContext.Tickets
                    .Where(ticket => ticket.AssigneeAgentId == agent.Id
                        && (ticket.Status == TicketStatus.Assigned || ticket.Status == TicketStatus.InProgress)) // BR-05
                    .Select(ticket => ticket.Priority)
                    .ToList(),
            })
            .ToListAsync(ct);

        return rows
            .Select(row => new AgentCandidate(row.Id, row.LastAssignedAt, row.OpenTicketPriorities))
            .ToList();
    }
}
