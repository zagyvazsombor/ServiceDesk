using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetMyTickets;

// AssigneeName: null while the ticket has no assignee.
public sealed record MyTicketDto(
    Guid Id,
    string Title,
    string CategoryName,
    string? AssigneeName,
    Priority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt);
