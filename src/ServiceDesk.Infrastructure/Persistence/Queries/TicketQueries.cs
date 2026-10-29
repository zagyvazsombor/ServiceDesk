using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Application.Tickets.GetTicketDetails;
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
