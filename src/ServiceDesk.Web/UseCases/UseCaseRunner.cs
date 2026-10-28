using Microsoft.AspNetCore.Components.Authorization;

namespace ServiceDesk.Web.UseCases;

// AGENTS.md §5: one use case = one DI scope = one DbContext. Registered per circuit, so the injected
// AuthenticationStateProvider is the one of the circuit.
public sealed class UseCaseRunner(
    AuthenticationStateProvider authenticationStateProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<UseCaseRunner> logger) : IUseCaseRunner
{
    public async Task<TResult> RunAsync<THandler, TResult>(
        Func<THandler, CancellationToken, Task<TResult>> useCase,
        CancellationToken ct = default) where THandler : notnull
    {
        ArgumentNullException.ThrowIfNull(useCase);

        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();

        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            scope.ServiceProvider.GetRequiredService<CurrentUser>().Initialize(authenticationState.User);
            var handler = scope.ServiceProvider.GetRequiredService<THandler>();

            return await useCase(handler, ct);
        }
        // A cancellation requested by the caller (e.g. a disposed component) is not unexpected.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // AGENTS.md §11: logged here, handled by the ErrorBoundary of the MainLayout.
            logger.LogError(exception, "Use case {Handler} failed with an unexpected exception", typeof(THandler).Name);
            throw;
        }
    }
}
