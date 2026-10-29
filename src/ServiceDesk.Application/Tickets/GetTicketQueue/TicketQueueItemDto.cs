using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetTicketQueue;

// AssigneeName: null while the ticket has no assignee.
public sealed record TicketQueueItemDto(
    Guid Id,
    string Title,
    string CategoryName,
    Priority Priority,
    TicketStatus Status,
    string? AssigneeName,
    string? ReporterEmail,
    DateTimeOffset CreatedAt);
