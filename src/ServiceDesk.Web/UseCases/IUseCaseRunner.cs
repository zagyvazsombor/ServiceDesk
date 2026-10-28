namespace ServiceDesk.Web.UseCases;

// AGENTS.md §5: the only way a page component calls a use case.
public interface IUseCaseRunner
{
    Task<TResult> RunAsync<THandler, TResult>(
        Func<THandler, CancellationToken, Task<TResult>> useCase,
        CancellationToken ct = default) where THandler : notnull;
}
