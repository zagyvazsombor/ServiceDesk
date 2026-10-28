using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Infrastructure.Persistence.Repositories;

internal sealed class TicketRepository(AppDbContext dbContext) : ITicketRepository
{
    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Tickets
            .Include(ticket => ticket.History.OrderBy(entry => entry.Sequence))
            .SingleOrDefaultAsync(ticket => ticket.Id == id, ct);

    public void Add(Ticket ticket) => dbContext.Tickets.Add(ticket);
}
