using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.CreateTicket;

// AGENTS.md §8: only the fields a Reporter may set; ReporterId, Status, AssigneeAgentId and Priority are not part of it.
// Impact and Urgency are nullable because their first value (High) would otherwise stand in for a missing choice.
public sealed record CreateTicketCommand(
    string Title,
    string Description,
    Impact? Impact,
    Urgency? Urgency,
    Guid CategoryId);
