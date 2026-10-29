using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Web.Components.Pages.Tickets;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages.Tickets;

public sealed class TicketDetailsTests : BunitContext
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly List<CancellationToken> _loadTokens = [];

    public TicketDetailsTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var authorization = AddAuthorization();
        authorization.SetAuthorized(TestUsers.Email);
        authorization.SetRoles(Roles.Reporter);
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public void Render_NotFoundResult_ShowsFriendlyNotFoundStateWithoutSnackbar()
    {
        // Arrange
        DetailsReturn(Task.FromResult<Result<TicketDetailsDto>>(TicketErrors.NotFound));

        // Act
        var cut = RenderDetails(Guid.CreateVersion7());

        // Assert
        cut.Find("#ticket-not-found").TextContent.ShouldContain("A jegy nem található.");
        cut.Find("a[href='tickets/my']").TextContent.Trim().ShouldBe("Vissza a jegyeimhez");
        _snackbar.DidNotReceiveWithAnyArgs().Add(default(string)!, default, default, default);
    }

    [Fact]
    public void Render_ForbiddenResult_ShowsSnackbarMessage()
    {
        // Arrange
        DetailsReturn(Task.FromResult<Result<TicketDetailsDto>>(AuthErrors.Forbidden));

        // Act
        var cut = RenderDetails(Guid.CreateVersion7());

        // Assert
        _snackbar.Received(1).Add(AuthErrors.Forbidden.Message, Severity.Error, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        cut.FindAll("#ticket-not-found").ShouldBeEmpty();
    }

    [Fact]
    public void Render_TicketWithMarkupInDescription_ShowsDescriptionAsPlainText()
    {
        // Arrange
        var ticket = Details("Nem indul a laptop", description: "<b>Fekete</b> képernyő\n<script>alert(1)</script>");
        DetailsReturn(Task.FromResult(Result.Success(ticket)));

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        var description = cut.Find("#ticket-description");
        description.TextContent.ShouldBe(ticket.Description);
        description.QuerySelector("b").ShouldBeNull();
        description.QuerySelector("script").ShouldBeNull();
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void Render_TicketWithHistory_ShowsHungarianValuesInHeaderAndTimeline()
    {
        // Arrange
        var ticket = Details("Nem indul a laptop", assigneeName: "Kiss Anna", status: TicketStatus.InProgress) with
        {
            History =
            [
                new TicketHistoryEntryDto(1, CreatedAt, TicketHistoryAction.Created, TestUsers.Email, null, "New"),
                new TicketHistoryEntryDto(2, CreatedAt, TicketHistoryAction.Assigned, null, null, "Kiss Anna"),
                new TicketHistoryEntryDto(3, CreatedAt.AddHours(1), TicketHistoryAction.StatusChanged, "Kiss Anna", "Assigned", "InProgress"),
            ],
        };
        DetailsReturn(Task.FromResult(Result.Success(ticket)));

        // Act
        var cut = RenderDetails(ticket.Id);

        // Assert
        cut.Find("#ticket-title").TextContent.ShouldBe("Nem indul a laptop");
        cut.Find("#ticket-assignee").TextContent.ShouldBe("Kiss Anna");
        cut.FindAll(".mud-chip").Select(chip => chip.TextContent.Trim()).ShouldBe(["Folyamatban", "Közepes"]);
        var items = cut.FindAll(".mud-timeline-item").Select(item => item.TextContent).ToList();
        items.Count.ShouldBe(3);
        items[0].ShouldContain("Jegy létrehozva");
        items[0].ShouldContain("Új");
        items[0].ShouldContain(TestUsers.Email);
        items[1].ShouldContain("Kiosztás");
        items[1].ShouldContain("Rendszer");
        items[2].ShouldContain("Kiosztva → Folyamatban");
        items[2].ShouldContain("2026. 10. 29. 10:00"); // 09:00 UTC in Budapest winter time
    }

    [Fact]
    public async Task SetParameters_NewIdWhileLoading_CancelsPreviousLoadAndShowsNewerTicket()
    {
        // Arrange
        var first = Details("Első jegy");
        var second = Details("Második jegy");
        var firstLoad = new TaskCompletionSource<Result<TicketDetailsDto>>();
        DetailsReturn(firstLoad.Task, Task.FromResult(Result.Success(second)));
        var cut = RenderDetails(first.Id);

        // Act
        cut.Render(parameters => parameters.Add(page => page.Id, second.Id));
        await cut.InvokeAsync(() => firstLoad.SetResult(Result.Success(first)));

        // Assert
        _loadTokens.Count.ShouldBe(2);
        _loadTokens[0].IsCancellationRequested.ShouldBeTrue();
        _loadTokens[1].IsCancellationRequested.ShouldBeFalse();
        cut.Find("#ticket-title").TextContent.ShouldBe("Második jegy");
    }

    [Fact]
    public async Task Dispose_WhileLoading_CancelsLoad()
    {
        // Arrange
        DetailsReturn(new TaskCompletionSource<Result<TicketDetailsDto>>().Task);
        RenderDetails(Guid.CreateVersion7());

        // Act
        await DisposeComponentsAsync();

        // Assert
        _loadTokens.ShouldHaveSingleItem().IsCancellationRequested.ShouldBeTrue();
    }

    private IRenderedComponent<TicketDetails> RenderDetails(Guid id) =>
        Render<TicketDetails>(parameters => parameters.Add(page => page.Id, id));

    // Answers the loads in order and records the token of each.
    private void DetailsReturn(params Task<Result<TicketDetailsDto>>[] answers)
    {
        var remaining = new Queue<Task<Result<TicketDetailsDto>>>(answers);
        _runner.RunAsync(
                Arg.Any<Func<GetTicketDetailsHandler, CancellationToken, Task<Result<TicketDetailsDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _loadTokens.Add(call.Arg<CancellationToken>());
                return remaining.Dequeue();
            });
    }

    private static TicketDetailsDto Details(
        string title,
        string description = "Bekapcsoláskor a laptop képernyője fekete marad.",
        string? assigneeName = null,
        TicketStatus status = TicketStatus.New) =>
        new(
            Guid.CreateVersion7(),
            title,
            description,
            Impact.Medium,
            Urgency.Medium,
            Priority.Medium,
            status,
            CreatedAt,
            TestUsers.Email,
            "HARDWARE",
            "Hardver",
            assigneeName,
            [new TicketHistoryEntryDto(1, CreatedAt, TicketHistoryAction.Created, TestUsers.Email, null, "New")]);
}
