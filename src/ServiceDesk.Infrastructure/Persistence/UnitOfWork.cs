using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Common;
using ServiceDesk.Infrastructure.Persistence.Configurations;

namespace ServiceDesk.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext dbContext, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    // SQL Server: duplicate key row in a unique index.
    private const int UniqueIndexViolation = 2601;

    public async Task<Result> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // AGENTS.md §7, §11: an expected conflict, reported to the user, logged as a warning.
            logger.LogWarning(
                exception,
                "Concurrency conflict while saving {EntityTypes}.",
                exception.Entries.Select(entry => entry.Metadata.ClrType.Name).Distinct());
            return GeneralErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (IsConcurrentHistoryAppend(exception))
        {
            // Every ticket change appends a history entry with the next sequence. EF may insert the entry before the
            // ticket UPDATE that would fail on RowVersion, so a concurrent change surfaces here first.
            logger.LogWarning(exception, "Concurrency conflict while appending ticket history.");
            return GeneralErrors.ConcurrencyConflict;
        }
    }

    private static bool IsConcurrentHistoryAppend(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation } sqlException
        && sqlException.Message.Contains(TicketHistoryEntryConfiguration.SequenceIndexName, StringComparison.Ordinal);
}
