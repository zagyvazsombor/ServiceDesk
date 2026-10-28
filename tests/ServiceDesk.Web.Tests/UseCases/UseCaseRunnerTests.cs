using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Common;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.UseCases;

// AGENTS.md §5: the runner is resolved from a circuit scope with the same registration as Program.cs.
public sealed class UseCaseRunnerTests : IAsyncDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider = Substitute.For<AuthenticationStateProvider>();
    private readonly RecordingLogger<UseCaseRunner> _logger = new();
    private readonly ServiceProvider _services;
    private readonly AsyncServiceScope _circuitScope;

    public UseCaseRunnerTests()
    {
        _services = new ServiceCollection()
            .AddUseCaseRunner()
            .AddScoped(_ => _authenticationStateProvider)
            .AddSingleton<ILogger<UseCaseRunner>>(_logger)
            .AddScoped<ProbeHandler>()
            .AddScoped<ScopedDependency>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _circuitScope = _services.CreateAsyncScope();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private IUseCaseRunner Runner => _circuitScope.ServiceProvider.GetRequiredService<IUseCaseRunner>();

    [Fact]
    public async Task RunAsync_SignedInAdmin_HandlerGetsUserOfCircuit()
    {
        // Arrange
        SignIn(TestUsers.Admin);

        // Act
        var probe = await Runner.RunAsync((ProbeHandler handler, CancellationToken ct) => handler.HandleAsync(ct), CancellationToken);

        // Assert
        probe.UserId.ShouldBe(TestUsers.UserId);
        probe.IsAdmin.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_AnonymousUser_HandlerGetsAnonymousUser()
    {
        // Arrange
        SignIn(TestUsers.Anonymous);

        // Act
        var probe = await Runner.RunAsync((ProbeHandler handler, CancellationToken ct) => handler.HandleAsync(ct), CancellationToken);

        // Assert
        probe.UserId.ShouldBeNull();
        probe.IsAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_SignedInUser_HandlerSeesCircuitUserInItsOwnScopeOnly()
    {
        // Arrange
        SignIn(TestUsers.Reporter);

        // Act
        var probe = await Runner.RunAsync((ProbeHandler handler, CancellationToken ct) => handler.HandleAsync(ct), CancellationToken);

        // Assert: the user is set on the CurrentUser of the use case scope, not on the one of the circuit scope.
        var circuitCurrentUser = _circuitScope.ServiceProvider.GetRequiredService<CurrentUser>();
        probe.CurrentUser.ShouldNotBeSameAs(circuitCurrentUser);
        probe.CurrentUser.UserId.ShouldBe(TestUsers.UserId);
        circuitCurrentUser.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_TwoCalls_EachRunsInItsOwnScopeThatIsDisposedAfterwards()
    {
        // Arrange
        SignIn(TestUsers.Reporter);

        // Act
        var first = await Runner.RunAsync((ProbeHandler handler, CancellationToken ct) => handler.HandleAsync(ct), CancellationToken);
        var second = await Runner.RunAsync((ProbeHandler handler, CancellationToken ct) => handler.HandleAsync(ct), CancellationToken);

        // Assert
        second.Dependency.ShouldNotBeSameAs(first.Dependency);
        first.Dependency.IsDisposed.ShouldBeTrue();
        second.Dependency.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_UserChangesBetweenCalls_EachCallSeesTheUserOfItsTime()
    {
        // Arrange: e.g. the revalidation signs the user out between two calls of the same circuit.
        SignIn(TestUsers.Admin);
        var first = await Runner.RunAsync((ProbeHandler handler, CancellationToken ct) => handler.HandleAsync(ct), CancellationToken);
        SignIn(TestUsers.Anonymous);

        // Act
        var second = await Runner.RunAsync((ProbeHandler handler, CancellationToken ct) => handler.HandleAsync(ct), CancellationToken);

        // Assert
        second.CurrentUser.ShouldNotBeSameAs(first.CurrentUser);
        first.UserId.ShouldBe(TestUsers.UserId);
        second.UserId.ShouldBeNull();
        second.IsAdmin.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_HandlerThrows_LogsErrorAndRethrows()
    {
        // Arrange
        SignIn(TestUsers.Reporter);
        var failure = new InvalidOperationException("Test failure.");

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            Runner.RunAsync<ProbeHandler, Probe>((_, _) => throw failure, CancellationToken));

        // Assert
        exception.ShouldBeSameAs(failure);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Exception.ShouldBeSameAs(failure);
        entry.Properties["Handler"].ShouldBe(nameof(ProbeHandler));
    }

    [Fact]
    public async Task RunAsync_HandlerThrows_DisposesScope()
    {
        // Arrange
        SignIn(TestUsers.Reporter);
        ProbeHandler? usedHandler = null;

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() =>
            Runner.RunAsync<ProbeHandler, Probe>(
                (handler, _) =>
                {
                    usedHandler = handler;
                    throw new InvalidOperationException("Test failure.");
                },
                CancellationToken));

        // Assert
        usedHandler.ShouldNotBeNull().Dependency.IsDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_CancelledByCaller_RethrowsWithoutErrorLog()
    {
        // Arrange
        SignIn(TestUsers.Reporter);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() =>
            Runner.RunAsync((ProbeHandler handler, CancellationToken ct) => handler.HandleAsync(ct), cts.Token));

        // Assert
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_ForbiddenResult_LogsWarningWithHandlerUserAndErrorCode()
    {
        // Arrange: a signed-in user without the required role.
        SignIn(TestUsers.Reporter);

        // Act
        var result = await Runner.RunAsync(
            (ProbeHandler _, CancellationToken _) => Task.FromResult<Result<Probe>>(AuthErrors.Forbidden), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Exception.ShouldBeNull();
        entry.Properties["Handler"].ShouldBe(nameof(ProbeHandler));
        entry.Properties["UserId"].ShouldBe(TestUsers.UserId);
        entry.Properties["ErrorCode"].ShouldBe(AuthErrors.Forbidden.Code);
    }

    [Fact]
    public async Task RunAsync_SuccessfulResult_LogsNothing()
    {
        // Arrange
        SignIn(TestUsers.Reporter);

        // Act
        var result = await Runner.RunAsync(
            (ProbeHandler _, CancellationToken _) => Task.FromResult(Result.Success()), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_FailureOtherThanForbidden_LogsNothing()
    {
        // Arrange: e.g. BR-02 answers a foreign ticket with NotFound; only Forbidden counts as a denied access here.
        SignIn(TestUsers.Reporter);
        var notFound = new Error("Test.NotFound", "Nem található.", ErrorType.NotFound);

        // Act
        var result = await Runner.RunAsync(
            (ProbeHandler _, CancellationToken _) => Task.FromResult(Result.Failure(notFound)), CancellationToken);

        // Assert
        result.Error.ShouldBe(notFound);
        _logger.Entries.ShouldBeEmpty();
    }

    public async ValueTask DisposeAsync()
    {
        await _circuitScope.DisposeAsync();
        await _services.DisposeAsync();
    }

    private void SignIn(ClaimsPrincipal user) =>
        _authenticationStateProvider.GetAuthenticationStateAsync().Returns(new AuthenticationState(user));

    // A handler in the shape of the Application handlers: it gets ICurrentUser and a scoped dependency.
    private sealed class ProbeHandler(ICurrentUser currentUser, ScopedDependency dependency)
    {
        public ScopedDependency Dependency => dependency;

        public Task<Probe> HandleAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new Probe(currentUser.UserId, currentUser.IsInRole(Roles.Admin), currentUser, dependency));
        }
    }

    private sealed class ScopedDependency : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed record Probe(string? UserId, bool IsAdmin, ICurrentUser CurrentUser, ScopedDependency Dependency);
}
