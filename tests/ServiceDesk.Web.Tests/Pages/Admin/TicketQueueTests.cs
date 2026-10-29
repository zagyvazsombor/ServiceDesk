using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.GetTicketQueue;
using ServiceDesk.Application.Tickets.GetTicketQueueFilters;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Web.Components.Pages.Admin;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages.Admin;

// The queue runs the real GetTicketQueueHandler on a replaced ITicketQueries, so the tests see the query of the filters.
public sealed class TicketQueueTests : BunitContext
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly QueueFilterOptionDto Hardware = new(Guid.CreateVersion7(), "Hardver", IsActive: true);
    private static readonly QueueFilterOptionDto Other = new(Guid.CreateVersion7(), "Egyéb", IsActive: false);
    private static readonly QueueFilterOptionDto Anna = new(Guid.CreateVersion7(), "Kiss Anna", IsActive: true);

    private static readonly TicketQueueItemDto Assigned = new(
        Guid.CreateVersion7(), "Nem indul a laptop", "Hardver", Priority.High, TicketStatus.InProgress, "Kiss Anna",
        TestUsers.Email, CreatedAt);

    private static readonly TicketQueueItemDto Unassigned = new(
        Guid.CreateVersion7(), "Nem érem el a VPN-t", "Hálózat", Priority.Low, TicketStatus.New, null,
        "bejelento.erik@servicedesk.local", CreatedAt.AddHours(-1));

    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();
    private readonly List<GetTicketQueueQuery> _queries = [];
    private int _totalCount = 2;

    public TicketQueueTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        JSInterop.Mode = JSRuntimeMode.Loose;

        var admin = Substitute.For<ICurrentUser>();
        admin.UserId.Returns(TestUsers.UserId);
        admin.IsAuthenticated.Returns(true);
        admin.IsInRole(Roles.Admin).Returns(true);
        var handler = new GetTicketQueueHandler(admin, new GetTicketQueueValidator(), _ticketQueries);

        _ticketQueries.GetTicketQueueAsync(Arg.Any<GetTicketQueueQuery>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetTicketQueueQuery>();
                _queries.Add(query);
                return new PagedResult<TicketQueueItemDto>([Assigned, Unassigned], query.PageNumber, call.Arg<int>(), _totalCount);
            });
        _runner.RunAsync(
                Arg.Any<Func<GetTicketQueueHandler, CancellationToken, Task<Result<PagedResult<TicketQueueItemDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<GetTicketQueueHandler, CancellationToken, Task<Result<PagedResult<TicketQueueItemDto>>>>>()(
                handler, call.Arg<CancellationToken>()));
        FiltersReturn(Result.Success(new TicketQueueFiltersDto([Other, Hardware], [Anna])));
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public void Render_DefaultFilters_QueriesFirstPageWithoutClosedTickets()
    {
        // Act
        var cut = Render<TicketQueue>();

        // Assert
        _queries.ShouldHaveSingleItem().ShouldBe(new GetTicketQueueQuery(PageNumber: 1));
        _queries[0].IncludeClosed.ShouldBeFalse();
        Switch(cut, "Lezártak is").Instance.GetState(x => x.Value).ShouldBeFalse();
    }

    [Fact]
    public void Render_Queue_ShowsRowsWithHungarianValues()
    {
        // Act
        var cut = Render<TicketQueue>();

        // Assert
        var rows = cut.FindAll("tbody tr");
        rows.Count.ShouldBe(2);
        Cells(rows[0]).ShouldBe(
            ["Nem indul a laptop", "Folyamatban", "Magas", "Hardver", "Kiss Anna", TestUsers.Email, "2026. 07. 15. 12:30"]);
        Cells(rows[1]).ShouldBe(
            ["Nem érem el a VPN-t", "Új", "Alacsony", "Hálózat", "Nincs kiosztva", "bejelento.erik@servicedesk.local", "2026. 07. 15. 11:30"]);
        rows[0].QuerySelectorAll(".mud-chip").Length.ShouldBe(2);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task SelectCategory_InactiveCategory_IsOfferedWithMarkerAndFiltersQuery()
    {
        // Arrange
        var cut = Render<TicketQueue>();
        var category = Select<Guid?>(cut, "Kategória");

        // Act
        await cut.InvokeAsync(() => category.Instance.SelectOption(Other.Id));

        // Assert
        category.Instance.ToStringFunc!(Other.Id).ShouldBe("Egyéb (inaktív)");
        _queries[^1].ShouldBe(new GetTicketQueueQuery(PageNumber: 1, CategoryId: Other.Id));
    }

    [Fact]
    public async Task UnassignedOnly_SwitchedOn_ClearsAndDisablesAssigneeFilter()
    {
        // Arrange
        var cut = Render<TicketQueue>();
        await cut.InvokeAsync(() => Select<Guid?>(cut, "Felelős").Instance.SelectOption(Anna.Id));
        _queries[^1].AssigneeAgentId.ShouldBe(Anna.Id);

        // Act
        Switch(cut, "Csak kiosztatlan").Find("input").Change(true);

        // Assert: the query does not accept the two filters together.
        _queries[^1].ShouldBe(new GetTicketQueueQuery(PageNumber: 1, UnassignedOnly: true));
        Select<Guid?>(cut, "Felelős").Instance.Disabled.ShouldBeTrue();
        _snackbar.DidNotReceiveWithAnyArgs().Add(default(string)!, default, default, default);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task IncludeClosed_SwitchedOffWithClosedStatus_ClearsStatusFilter()
    {
        // Arrange
        var cut = Render<TicketQueue>();
        Switch(cut, "Lezártak is").Find("input").Change(true);
        await cut.InvokeAsync(() => Select<TicketStatus?>(cut, "Státusz").Instance.SelectOption(TicketStatus.Closed));
        _queries[^1].ShouldBe(new GetTicketQueueQuery(PageNumber: 1, Status: TicketStatus.Closed, IncludeClosed: true));

        // Act
        Switch(cut, "Lezártak is").Find("input").Change(false);

        // Assert
        _queries[^1].ShouldBe(new GetTicketQueueQuery(PageNumber: 1));
        Select<TicketStatus?>(cut, "Státusz").Instance.GetState(x => x.Value).ShouldBeNull();
        _snackbar.DidNotReceiveWithAnyArgs().Add(default(string)!, default, default, default);
    }

    [Fact]
    public async Task SelectPriority_OnSecondPage_QueriesFirstPageAgain()
    {
        // Arrange
        _totalCount = 45;
        var cut = Render<TicketQueue>();
        await cut.InvokeAsync(() => cut.FindComponent<MudDataGrid<TicketQueueItemDto>>().Instance.NavigateTo(Page.Next));
        cut.WaitForAssertion(() => _queries[^1].PageNumber.ShouldBe(2));

        // Act
        await cut.InvokeAsync(() => Select<Priority?>(cut, "Prioritás").Instance.SelectOption(Priority.High));

        // Assert
        cut.WaitForAssertion(() => _queries[^1].ShouldBe(new GetTicketQueueQuery(PageNumber: 1, Priority: Priority.High)));
    }

    [Fact]
    public void RowClick_Ticket_NavigatesToTicketDetails()
    {
        // Arrange
        var cut = Render<TicketQueue>();

        // Act
        cut.FindAll("tbody tr")[1].Click();

        // Assert
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe($"http://localhost/tickets/{Unassigned.Id}");
    }

    [Fact]
    public void Render_FiltersFailed_ShowsSnackbarAndStillListsTickets()
    {
        // Arrange
        FiltersReturn(new Error("General.Test", "A szűrők nem tölthetők be.", ErrorType.Unavailable));

        // Act
        var cut = Render<TicketQueue>();

        // Assert
        _snackbar.Received(1).Add("A szűrők nem tölthetők be.", Severity.Error, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        cut.FindAll("tbody tr").Count.ShouldBe(2);
    }

    private void FiltersReturn(Result<TicketQueueFiltersDto> result) =>
        _runner.RunAsync(
                Arg.Any<Func<GetTicketQueueFiltersHandler, CancellationToken, Task<Result<TicketQueueFiltersDto>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(result);

    private static IRenderedComponent<MudSelect<T>> Select<T>(IRenderedComponent<TicketQueue> cut, string label) =>
        cut.FindComponents<MudSelect<T>>().Single(select => select.Instance.Label == label);

    private static IRenderedComponent<MudSwitch<bool>> Switch(IRenderedComponent<TicketQueue> cut, string label) =>
        cut.FindComponents<MudSwitch<bool>>().Single(toggle => toggle.Instance.Label == label);

    private static List<string> Cells(AngleSharp.Dom.IElement row) =>
        row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToList();
}
