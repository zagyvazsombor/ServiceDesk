using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDesk.Application;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Users.GetCurrentUserProfile;
using ServiceDesk.Domain.Common;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.UseCases;

// The runner with the real Application registration (AddApplication): the handler reads the profile of the circuit user,
// and the denied access of an anonymous user is logged by the runner.
public sealed class GetCurrentUserProfileRunnerTests : IAsyncDisposable
{
    private readonly AuthenticationStateProvider _authenticationStateProvider = Substitute.For<AuthenticationStateProvider>();
    private readonly IUserQueries _userQueries = Substitute.For<IUserQueries>();
    private readonly RecordingLogger<UseCaseRunner> _logger = new();
    private readonly ServiceProvider _services;
    private readonly AsyncServiceScope _circuitScope;

    public GetCurrentUserProfileRunnerTests()
    {
        _services = new ServiceCollection()
            .AddApplication()
            .AddUseCaseRunner()
            .AddScoped(_ => _authenticationStateProvider)
            .AddScoped(_ => _userQueries)
            .AddSingleton<ILogger<UseCaseRunner>>(_logger)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _circuitScope = _services.CreateAsyncScope();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_SignedInAdmin_ReturnsProfileOfCircuitUser()
    {
        // Arrange
        SignIn(TestUsers.Admin);
        var profile = new CurrentUserProfileDto(TestUsers.UserId, TestUsers.Email, [Roles.Admin], new AgentProfileDto("Kiss Anna", true));
        _userQueries.GetProfileAsync(TestUsers.UserId, Arg.Any<CancellationToken>()).Returns(profile);

        // Act
        var result = await RunGetCurrentUserProfileAsync();

        // Assert
        result.Value.ShouldBeSameAs(profile);
        await _userQueries.Received(1).GetProfileAsync(TestUsers.UserId, Arg.Any<CancellationToken>());
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_AnonymousUser_ReturnsForbiddenAndLogsWarning()
    {
        // Arrange
        SignIn(TestUsers.Anonymous);

        // Act
        var result = await RunGetCurrentUserProfileAsync();

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _userQueries.DidNotReceiveWithAnyArgs().GetProfileAsync(default!, CancellationToken);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["Handler"].ShouldBe(nameof(GetCurrentUserProfileHandler));
        entry.Properties["UserId"].ShouldBeNull();
        entry.Properties["ErrorCode"].ShouldBe(AuthErrors.Forbidden.Code);
    }

    public async ValueTask DisposeAsync()
    {
        await _circuitScope.DisposeAsync();
        await _services.DisposeAsync();
    }

    private Task<Result<CurrentUserProfileDto>> RunGetCurrentUserProfileAsync() =>
        _circuitScope.ServiceProvider.GetRequiredService<IUseCaseRunner>().RunAsync(
            (GetCurrentUserProfileHandler handler, CancellationToken ct) => handler.HandleAsync(new GetCurrentUserProfileQuery(), ct),
            CancellationToken);

    private void SignIn(ClaimsPrincipal user) =>
        _authenticationStateProvider.GetAuthenticationStateAsync().Returns(new AuthenticationState(user));
}
