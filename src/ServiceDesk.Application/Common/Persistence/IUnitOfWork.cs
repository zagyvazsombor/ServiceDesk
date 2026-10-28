using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Persistence;

public interface IUnitOfWork
{
    // Saves every change of the current use case; a concurrency conflict is GeneralErrors.ConcurrencyConflict.
    Task<Result> SaveChangesAsync(CancellationToken ct);
}
