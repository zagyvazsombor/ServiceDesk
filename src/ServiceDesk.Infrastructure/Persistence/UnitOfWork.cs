using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Categories;
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
        catch (DbUpdateException exception) when (IsDuplicateCategoryCode(exception))
        {
            // The validator checks the code before the save; two categories created with the same code at the same time
            // both pass it, and the unique index refuses the second one.
            logger.LogWarning(exception, "Duplicate category code while saving.");
            return CategoryErrors.CodeAlreadyExists;
        }
    }

    private static bool IsConcurrentHistoryAppend(DbUpdateException exception) =>
        IsUniqueIndexViolation(exception, TicketHistoryEntryConfiguration.SequenceIndexName);

    private static bool IsDuplicateCategoryCode(DbUpdateException exception) =>
        IsUniqueIndexViolation(exception, CategoryConfiguration.CodeIndexName);

    private static bool IsUniqueIndexViolation(DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation } sqlException
        && sqlException.Message.Contains(indexName, StringComparison.Ordinal);
}
