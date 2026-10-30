using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using ServiceDesk.Web.Components.Layout;
using ServiceDesk.Web.Tests.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Layout;

// AGENTS.md §8, level 1, §11: a page refused by its role inside the circuit shows a message and logs a Warning.
public sealed class NotAuthorizedAlertTests : BunitContext
{
    private readonly RecordingLogger<NotAuthorizedAlert> _logger = new();

    public NotAuthorizedAlertTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<ILogger<NotAuthorizedAlert>>(_logger);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Render_SignedInUserWithoutRole_ShowsMessageAndLogsWarningWithPathAndUser()
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("admin/queue?page=2");

        // Act
        var component = Render<NotAuthorizedAlert>(parameters => parameters.Add(alert => alert.User, TestUsers.Reporter));

        // Assert
        component.Markup.ShouldContain("Nincs jogosultságod az oldal megtekintéséhez.");
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["Path"].ShouldBe("/admin/queue"); // without the query string
        entry.Properties["UserId"].ShouldBe(TestUsers.UserId);
    }
}
