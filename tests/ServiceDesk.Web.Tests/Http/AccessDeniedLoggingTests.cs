using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using ServiceDesk.Web.Security;
using Shouldly;

namespace ServiceDesk.Web.Tests.Http;

// AGENTS.md §8, level 1, §11: the real pipeline logs a page refused by its role as a Warning (the test user is a
// Reporter), and still sends the user to the AccessDenied page.
public sealed class AccessDeniedLoggingTests(ServiceDeskWebApplicationFactory factory) : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private static readonly string HandlerCategory = typeof(AccessDeniedLoggingResultHandler).FullName!;

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_AdminPageAsReporter_RedirectsToAccessDeniedAndLogsWarning()
    {
        // Arrange
        var logs = new RecordingLoggerProvider();
        await using var server = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = server.CreateClient(new() { AllowAutoRedirect = false });

        // Act
        using var response = await client.GetAsync("/admin/queue", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldContain("/Account/AccessDenied");
        var entry = logs.EntriesOf(HandlerCategory).ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["Path"].ShouldBe("/admin/queue");
        entry.Properties["UserId"].ShouldBe(TestUsers.UserId);
    }

    [Fact]
    public async Task Get_PageOfOwnRole_LogsNoDenial()
    {
        // Arrange
        var logs = new RecordingLoggerProvider();
        await using var server = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = server.CreateClient(new() { AllowAutoRedirect = false });

        // Act
        using var response = await client.GetAsync("/tickets/my", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        logs.EntriesOf(HandlerCategory).ShouldBeEmpty();
    }
}
