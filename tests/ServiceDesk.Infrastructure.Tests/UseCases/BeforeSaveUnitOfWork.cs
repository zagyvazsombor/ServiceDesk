using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Infrastructure.Tests.UseCases;

// Runs a competing use case right before the save of the decorated one. By then the decorated use case has loaded and
// changed the ticket, so the two always overlap the same way, without timing or threads.
internal sealed class BeforeSaveUnitOfWork(IUnitOfWork inner, Func<Task> beforeSave) : IUnitOfWork
{
    public async Task<Result> SaveChangesAsync(CancellationToken ct)
    {
        await beforeSave();
        return await inner.SaveChangesAsync(ct);
    }
}
