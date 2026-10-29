using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Persistence;

public interface IUnitOfWork
{
    // Saves every change of the current use case; a concurrency conflict is GeneralErrors.ConcurrencyConflict, and a
    // category code saved concurrently by someone else is CategoryErrors.CodeAlreadyExists.
    Task<Result> SaveChangesAsync(CancellationToken ct);
}
