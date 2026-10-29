using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetTicketDetails;

// AssigneeName: null while the ticket has no assignee. History: in sequence order (BR-10).
public sealed record TicketDetailsDto(
    Guid Id,
    string Title,
    string Description,
    Impact Impact,
    Urgency Urgency,
    Priority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt,
    string? ReporterEmail,
    string CategoryCode,
    string CategoryName,
    string? AssigneeName,
    IReadOnlyList<TicketHistoryEntryDto> History);
