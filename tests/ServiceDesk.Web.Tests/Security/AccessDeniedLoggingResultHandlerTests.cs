using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Web.Security;
using ServiceDesk.Web.Tests.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Security;

// AGENTS.md §11: a page refused by its role (level 1) is a Warning; the default handler still gives the answer.
public sealed class AccessDeniedLoggingResultHandlerTests
{
    private static readonly AuthorizationPolicy AdminPolicy = new AuthorizationPolicyBuilder().RequireRole(Roles.Admin).Build();

    private readonly RecordingLogger<AccessDeniedLoggingResultHandler> _logger = new();
    private readonly IAuthenticationService _authentication = Substitute.For<IAuthenticationService>();
    private bool _nextCalled;

    [Fact]
    public async Task HandleAsync_Forbidden_LogsWarningWithPathAndUserAndForbids()
    {
        // Arrange
        var context = CreateContext("/admin/queue", "?page=2");

        // Act
        await CreateHandler().HandleAsync(Next, context, AdminPolicy, PolicyAuthorizationResult.Forbid());

        // Assert
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["Path"].ShouldBe("/admin/queue"); // without the query string
        entry.Properties["UserId"].ShouldBe(TestUsers.UserId);
        await _authentication.Received(1).ForbidAsync(context, null, null);
        _nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_ChallengeOfAnonymousUser_ChallengesWithoutLog()
    {
        // Arrange
        var context = CreateContext("/admin/queue");
        context.User = TestUsers.Anonymous;

        // Act
        await CreateHandler().HandleAsync(Next, context, AdminPolicy, PolicyAuthorizationResult.Challenge());

        // Assert: the way to the login page is not a denied access.
        _logger.Entries.ShouldBeEmpty();
        await _authentication.Received(1).ChallengeAsync(context, null, null);
        _nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_Success_CallsNextWithoutLog()
    {
        // Arrange
        var context = CreateContext("/tickets/my");

        // Act
        await CreateHandler().HandleAsync(Next, context, AdminPolicy, PolicyAuthorizationResult.Success());

        // Assert
        _logger.Entries.ShouldBeEmpty();
        _nextCalled.ShouldBeTrue();
    }

    private DefaultHttpContext CreateContext(string path, string query = "")
    {
        var services = new ServiceCollection().AddSingleton(_authentication).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, User = TestUsers.Reporter };
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        return context;
    }

    private Task Next(HttpContext context)
    {
        _nextCalled = true;
        return Task.CompletedTask;
    }

    private AccessDeniedLoggingResultHandler CreateHandler() => new(_logger);
}
