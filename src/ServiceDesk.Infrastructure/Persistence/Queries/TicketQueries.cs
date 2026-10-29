using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Agents.ListAgents;
using ServiceDesk.Application.Categories.ListCategories;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.GetAssignableAgents;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Application.Tickets.GetTicketQueue;
using ServiceDesk.Application.Tickets.GetTicketQueueFilters;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Infrastructure.Persistence.Queries;

internal sealed class TicketQueries(AppDbContext dbContext) : ITicketQueries
{
    public async Task<PagedResult<MyTicketDto>> GetMyTicketsAsync(
        string reporterId,
        int pageNumber,
        int pageSize,
        CancellationToken ct)
    {
        var ownTickets = dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.ReporterId == reporterId); // BR-02

        var totalCount = await ownTickets.CountAsync(ct);
        var items = await ownTickets
            .Join(
                dbContext.Categories,
                ticket => ticket.CategoryId,
                category => category.Id,
                (ticket, category) => new { Ticket = ticket, CategoryName = category.Name })
            // Newest first; the Id makes the order of tickets created at the same moment stable across pages.
            .OrderByDescending(row => row.Ticket.CreatedAt)
            .ThenByDescending(row => row.Ticket.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            // The assignee name is a subquery of the same SQL statement, as in GetTicketDetailsAsync (no N+1).
            .Select(row => new MyTicketDto(
                row.Ticket.Id,
                row.Ticket.Title,
                row.CategoryName,
                dbContext.Agents
                    .Where(agent => agent.Id == row.Ticket.AssigneeAgentId)
                    .Select(agent => agent.DisplayName)
                    .FirstOrDefault(),
                row.Ticket.Priority,
                row.Ticket.Status,
                row.Ticket.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<MyTicketDto>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<ActiveCategoryDto>> GetActiveCategoriesAsync(CancellationToken ct) =>
        await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsActive) // BR-13
            .OrderBy(category => category.Name)
            .Select(category => new ActiveCategoryDto(category.Id, category.Code, category.Name, category.Description))
            .ToListAsync(ct);

    // One query for the ticket with its history, then one each for the names the history refers to (no N+1).
    public async Task<TicketDetailsDto?> GetTicketDetailsAsync(Guid ticketId, string? reporterId, CancellationToken ct)
    {
        var tickets = dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == ticketId);
        if (reporterId is not null)
        {
            tickets = tickets.Where(ticket => ticket.ReporterId == reporterId); // BR-02, AGENTS.md §8 level 3
        }

        var details = await tickets
            .Join(
                dbContext.Categories,
                ticket => ticket.CategoryId,
                category => category.Id,
                (ticket, category) => new { Ticket = ticket, Category = category })
            .Select(row => new
            {
                row.Ticket.Id,
                row.Ticket.Title,
                row.Ticket.Description,
                row.Ticket.Impact,
                row.Ticket.Urgency,
                row.Ticket.Priority,
                row.Ticket.Status,
                row.Ticket.CreatedAt,
                ReporterEmail = dbContext.Users
                    .Where(user => user.Id == row.Ticket.ReporterId)
                    .Select(user => user.Email)
                    .FirstOrDefault(),
                CategoryCode = row.Category.Code,
                CategoryName = row.Category.Name,
                AssigneeName = dbContext.Agents
                    .Where(agent => agent.Id == row.Ticket.AssigneeAgentId)
                    .Select(agent => agent.DisplayName)
                    .FirstOrDefault(),
                History = row.Ticket.History
                    .OrderBy(entry => entry.Sequence)
                    .Select(entry => new HistoryRow(
                        entry.Sequence,
                        entry.At,
                        entry.Action,
                        entry.ActorUserId,
                        entry.OldValue,
                        entry.NewValue))
                    .ToList(),
            })
            .SingleOrDefaultAsync(ct);
        if (details is null)
        {
            return null;
        }

        var actorNames = await GetActorNamesAsync(details.History, ct);
        var agentNames = await GetAgentNamesAsync(ReferencedIds(details.History, RefersToAgent), ct);
        var categoryNames = await GetCategoryNamesAsync(ReferencedIds(details.History, RefersToCategory), ct);

        string? ResolveValue(TicketHistoryAction action, string? value)
        {
            var names = RefersToAgent(action) ? agentNames
                : RefersToCategory(action) ? categoryNames
                : null;
            return names is not null && Guid.TryParse(value, out var id) && names.TryGetValue(id, out var name)
                ? name
                : value;
        }

        return new TicketDetailsDto(
            details.Id,
            details.Title,
            details.Description,
            details.Impact,
            details.Urgency,
            details.Priority,
            details.Status,
            details.CreatedAt,
            details.ReporterEmail,
            details.CategoryCode,
            details.CategoryName,
            details.AssigneeName,
            details.History
                .Select(entry => new TicketHistoryEntryDto(
                    entry.Sequence,
                    entry.At,
                    entry.Action,
                    entry.ActorUserId is null ? null : actorNames.GetValueOrDefault(entry.ActorUserId),
                    ResolveValue(entry.Action, entry.OldValue),
                    ResolveValue(entry.Action, entry.NewValue)))
                .ToList());
    }

    public async Task<PagedResult<TicketQueueItemDto>> GetTicketQueueAsync(
        GetTicketQueueQuery query,
        int pageSize,
        CancellationToken ct)
    {
        var tickets = dbContext.Tickets.AsNoTracking();
        if (query.Status is { } status)
        {
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (query.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(ticket => ticket.CategoryId == categoryId);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(ticket => ticket.Priority == priority);
        }

        if (query.AssigneeAgentId is { } assigneeAgentId)
        {
            tickets = tickets.Where(ticket => ticket.AssigneeAgentId == assigneeAgentId);
        }

        if (query.UnassignedOnly)
        {
            tickets = tickets.Where(ticket => ticket.AssigneeAgentId == null);
        }

        if (!query.IncludeClosed)
        {
            tickets = tickets.Where(ticket => ticket.Status != TicketStatus.Closed);
        }

        var totalCount = await tickets.CountAsync(ct);
        var items = await tickets
            .Join(
                dbContext.Categories,
                ticket => ticket.CategoryId,
                category => category.Id,
                (ticket, category) => new { Ticket = ticket, CategoryName = category.Name })
            // The priority is stored by name, so its rank is spelled out (a CASE in SQL): Critical first.
            .OrderBy(row => row.Ticket.Priority == Priority.Critical ? 0
                : row.Ticket.Priority == Priority.High ? 1
                : row.Ticket.Priority == Priority.Medium ? 2
                : row.Ticket.Priority == Priority.Low ? 3
                : 4)
            // Within a priority the longest waiting ticket comes first; the Id keeps the page boundaries stable.
            .ThenBy(row => row.Ticket.CreatedAt)
            .ThenBy(row => row.Ticket.Id)
            .Skip((query.PageNumber - 1) * pageSize)
            .Take(pageSize)
            // The names are subqueries of the same SQL statement (no N+1).
            .Select(row => new TicketQueueItemDto(
                row.Ticket.Id,
                row.Ticket.Title,
                row.CategoryName,
                row.Ticket.Priority,
                row.Ticket.Status,
                dbContext.Agents
                    .Where(agent => agent.Id == row.Ticket.AssigneeAgentId)
                    .Select(agent => agent.DisplayName)
                    .FirstOrDefault(),
                dbContext.Users
                    .Where(user => user.Id == row.Ticket.ReporterId)
                    .Select(user => user.Email)
                    .FirstOrDefault(),
                row.Ticket.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<TicketQueueItemDto>(items, query.PageNumber, pageSize, totalCount);
    }

    // Two queries, one per list; the inactive entries are kept, as they can still be on tickets (BR-12, BR-13).
    public async Task<TicketQueueFiltersDto> GetTicketQueueFiltersAsync(CancellationToken ct)
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new QueueFilterOptionDto(category.Id, category.Name, category.IsActive))
            .ToListAsync(ct);
        var agents = await dbContext.Agents
            .AsNoTracking()
            .OrderBy(agent => agent.DisplayName)
            .Select(agent => new QueueFilterOptionDto(agent.Id, agent.DisplayName, agent.IsActive))
            .ToListAsync(ct);

        return new TicketQueueFiltersDto(categories, agents);
    }

    // One query for the ticket and one for the agents with the priorities of their open tickets (no N+1).
    public async Task<AssignableAgentsDto?> GetAssignableAgentsAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await dbContext.Tickets
            .AsNoTracking()
            .Where(row => row.Id == ticketId)
            .Select(row => new { row.Status, row.CategoryId, row.AssigneeAgentId })
            .SingleOrDefaultAsync(ct);
        if (ticket is null)
        {
            return null;
        }

        var rows = await dbContext.Agents
            .AsNoTracking()
            .Where(agent => agent.IsActive
                && agent.Competencies.Any(competency => competency.CategoryId == ticket.CategoryId)) // BR-04
            .OrderBy(agent => agent.DisplayName)
            .Select(agent => new
            {
                agent.Id,
                agent.DisplayName,
                agent.LastAssignedAt,
                OpenTicketPriorities = dbContext.Tickets
                    .Where(openTicket => openTicket.AssigneeAgentId == agent.Id
                        && (openTicket.Status == TicketStatus.Assigned || openTicket.Status == TicketStatus.InProgress))
                    .Select(openTicket => openTicket.Priority)
                    .ToList(),
            })
            .ToListAsync(ct);

        var agents = rows
            .Select(row => new AssignableAgentDto(
                row.Id,
                row.DisplayName,
                // BR-05: the same load as the automatic assignment uses.
                new AgentCandidate(row.Id, row.LastAssignedAt, row.OpenTicketPriorities).Load,
                row.OpenTicketPriorities.Count,
                row.Id == ticket.AssigneeAgentId))
            // A stable sort, so agents with the same load stay in name order.
            .OrderBy(agent => agent.Load)
            .ToList();

        return new AssignableAgentsDto(ticket.Status, agents);
    }

    public async Task<IReadOnlyList<CategoryListItemDto>> GetCategoriesAsync(CancellationToken ct) =>
        await dbContext.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryListItemDto(
                category.Id,
                category.Code,
                category.Name,
                category.Description,
                category.IsActive))
            .ToListAsync(ct);

    // A split query: one statement for the agents and one for each collection, so there is no N+1 and the competencies
    // and the open tickets of an agent do not multiply each other's rows.
    public async Task<IReadOnlyList<AgentListItemDto>> GetAgentsAsync(CancellationToken ct)
    {
        var rows = await dbContext.Agents
            .AsNoTracking()
            .OrderBy(agent => agent.DisplayName)
            .ThenBy(agent => agent.Id)
            .Select(agent => new
            {
                agent.Id,
                agent.DisplayName,
                agent.IsActive,
                agent.LastAssignedAt,
                Competencies = agent.Competencies
                    .Join(
                        dbContext.Categories,
                        competency => competency.CategoryId,
                        category => category.Id,
                        (competency, category) => category)
                    .OrderBy(category => category.Name)
                    .Select(category => new AgentCompetencyDto(category.Id, category.Code, category.Name, category.IsActive))
                    .ToList(),
                OpenTicketPriorities = dbContext.Tickets
                    .Where(ticket => ticket.AssigneeAgentId == agent.Id
                        && (ticket.Status == TicketStatus.Assigned || ticket.Status == TicketStatus.InProgress)) // BR-05
                    .Select(ticket => ticket.Priority)
                    .ToList(),
            })
            .AsSplitQuery()
            .ToListAsync(ct);

        return rows
            .Select(row => new AgentListItemDto(
                row.Id,
                row.DisplayName,
                row.IsActive,
                row.Competencies,
                // BR-05: the same load as the automatic assignment uses; an inactive agent keeps its tickets (BR-12).
                new AgentCandidate(row.Id, row.LastAssignedAt, row.OpenTicketPriorities).Load,
                row.OpenTicketPriorities.Count))
            .ToList();
    }

    // The entries whose old and new values are agent Ids, or category Ids (see Ticket).
    private static bool RefersToAgent(TicketHistoryAction action) =>
        action is TicketHistoryAction.Assigned or TicketHistoryAction.Reassigned or TicketHistoryAction.Unassigned;

    private static bool RefersToCategory(TicketHistoryAction action) => action is TicketHistoryAction.Recategorized;

    private static List<Guid> ReferencedIds(List<HistoryRow> history, Func<TicketHistoryAction, bool> refersTo) =>
        history
            .Where(entry => refersTo(entry.Action))
            .SelectMany(entry => new[] { entry.OldValue, entry.NewValue })
            .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToList();

    // An Admin is shown by their agent name, a Reporter by their e-mail address. Every ticket has at least the
    // Created entry of its reporter, so the list is never empty.
    private async Task<Dictionary<string, string?>> GetActorNamesAsync(List<HistoryRow> history, CancellationToken ct)
    {
        var actorUserIds = history.Select(entry => entry.ActorUserId).OfType<string>().Distinct().ToList();
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => actorUserIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                Name = dbContext.Agents
                    .Where(agent => agent.UserId == user.Id)
                    .Select(agent => agent.DisplayName)
                    .FirstOrDefault() ?? user.Email,
            })
            .ToDictionaryAsync(user => user.Id, user => user.Name, ct);
    }

    private async Task<Dictionary<Guid, string>> GetAgentNamesAsync(List<Guid> agentIds, CancellationToken ct) =>
        agentIds.Count == 0
            ? []
            : await dbContext.Agents
                .AsNoTracking()
                .Where(agent => agentIds.Contains(agent.Id))
                .ToDictionaryAsync(agent => agent.Id, agent => agent.DisplayName, ct);

    private async Task<Dictionary<Guid, string>> GetCategoryNamesAsync(List<Guid> categoryIds, CancellationToken ct) =>
        categoryIds.Count == 0
            ? []
            : await dbContext.Categories
                .AsNoTracking()
                .Where(category => categoryIds.Contains(category.Id))
                .ToDictionaryAsync(category => category.Id, category => category.Name, ct);

    private sealed record HistoryRow(
        int Sequence,
        DateTimeOffset At,
        TicketHistoryAction Action,
        string? ActorUserId,
        string? OldValue,
        string? NewValue);
}
