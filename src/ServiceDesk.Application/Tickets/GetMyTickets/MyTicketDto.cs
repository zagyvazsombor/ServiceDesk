using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.GetMyTickets;

public sealed record MyTicketDto(
    Guid Id,
    string Title,
    string CategoryName,
    Priority Priority,
    TicketStatus Status,
    DateTimeOffset CreatedAt);
