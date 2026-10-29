using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Web.Components.Pages.Tickets;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages.Tickets;

public sealed class MyTicketsTests : BunitContext
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly MyTicketDto Assigned = new(
        Guid.CreateVersion7(), "Nem indul a laptop", "Hardver", "Kiss Anna", Priority.High, TicketStatus.InProgress, CreatedAt);

    private static readonly MyTicketDto Unassigned = new(
        Guid.CreateVersion7(), "Nem érem el a VPN-t", "Hálózat", null, Priority.Low, TicketStatus.New, CreatedAt.AddHours(-1));

    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();

    public MyTicketsTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public void Render_OwnTickets_ShowsRowsWithHungarianValues()
    {
        // Arrange
        MyTicketsReturn(Result.Success(new PagedResult<MyTicketDto>([Assigned, Unassigned], 1, GetMyTicketsQuery.PageSize, 2)));

        // Act
        var cut = Render<MyTickets>();

        // Assert
        var rows = cut.FindAll("tbody tr");
        rows.Count.ShouldBe(2);
        Cells(rows[0]).ShouldBe(["Nem indul a laptop", "Folyamatban", "Magas", "Hardver", "Kiss Anna", "2026. 07. 15. 12:30"]);
        Cells(rows[1]).ShouldBe(["Nem érem el a VPN-t", "Új", "Alacsony", "Hálózat", "Nincs kiosztva", "2026. 07. 15. 11:30"]);
    }

    [Fact]
    public void RowClick_Ticket_NavigatesToTicketDetails()
    {
        // Arrange
        MyTicketsReturn(Result.Success(new PagedResult<MyTicketDto>([Assigned, Unassigned], 1, GetMyTicketsQuery.PageSize, 2)));
        var cut = Render<MyTickets>();

        // Act
        cut.FindAll("tbody tr")[1].Click();

        // Assert
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe($"http://localhost/tickets/{Unassigned.Id}");
    }

    [Fact]
    public void Render_NoTickets_ShowsEmptyMessage()
    {
        // Arrange
        MyTicketsReturn(Result.Success(new PagedResult<MyTicketDto>([], 1, GetMyTicketsQuery.PageSize, 0)));

        // Act
        var cut = Render<MyTickets>();

        // Assert
        cut.Markup.ShouldContain("Még nincs jegyed.");
    }

    private void MyTicketsReturn(Result<PagedResult<MyTicketDto>> result) =>
        _runner.RunAsync(
                Arg.Any<Func<GetMyTicketsHandler, CancellationToken, Task<Result<PagedResult<MyTicketDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(result);

    private static List<string> Cells(AngleSharp.Dom.IElement row) =>
        row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToList();
}
