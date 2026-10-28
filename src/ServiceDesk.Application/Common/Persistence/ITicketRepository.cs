using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Common.Persistence;

public interface ITicketRepository
{
    // Loads the whole aggregate, including its history in sequence order; null if the ticket does not exist.
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct);

    void Add(Ticket ticket);
}
