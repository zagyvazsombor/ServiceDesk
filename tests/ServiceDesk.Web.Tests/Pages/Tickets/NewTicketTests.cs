using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using ServiceDesk.Application.Tickets.CreateTicket;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Web.Components.Pages.Tickets;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.Web.Tests.Pages.Tickets;

public sealed class NewTicketTests : BunitContext
{
    private const string SubmitText = "Jegy rögzítése";

    private static readonly ActiveCategoryDto Hardware =
        new(Guid.CreateVersion7(), "HARDWARE", "Hardver", "Számítógépek, perifériák és egyéb eszközök.");

    private readonly IUseCaseRunner _runner = Substitute.For<IUseCaseRunner>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();

    public NewTicketTests()
    {
        Services.AddMudServices();
        Services.AddSingleton(_runner);
        Services.AddSingleton(_snackbar);
        JSInterop.Mode = JSRuntimeMode.Loose;

        _runner.RunAsync(
                Arg.Any<Func<GetActiveCategoriesHandler, CancellationToken, Task<Result<IReadOnlyList<ActiveCategoryDto>>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ActiveCategoryDto>>([Hardware]));
    }

    [Fact]
    public async Task Submit_EmptyFields_DoesNotRunCreateTicketAndShowsRequiredErrors()
    {
        // Arrange
        var cut = Render<NewTicket>();

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        await _runner.DidNotReceiveWithAnyArgs().RunAsync<CreateTicketHandler, Result<Guid>>(
            default!, Xunit.TestContext.Current.CancellationToken);
        cut.Markup.ShouldContain("Add meg a jegy címét.");
        cut.Markup.ShouldContain("Írd le a problémát.");
        cut.Markup.ShouldContain("Válaszd ki a hatást.");
        cut.Markup.ShouldContain("Válaszd ki a sürgősséget.");
        cut.Markup.ShouldContain("Válassz kategóriát.");
    }

    [Fact]
    public async Task Submit_SuccessfulResult_ShowsSnackbarAndNavigatesToTicketDetails()
    {
        // Arrange
        var ticketId = Guid.CreateVersion7();
        CreateTicketReturns(ticketId);
        var cut = Render<NewTicket>();
        await FillFormAsync(cut);

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        _snackbar.Received(1).Add("A jegy rögzítve.", Severity.Success, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe($"http://localhost/tickets/{ticketId}");
    }

    [Fact]
    public async Task Submit_FailedResult_ShowsSnackbarMessageAndStaysOnPage()
    {
        // Arrange
        CreateTicketReturns(CategoryErrors.Inactive);
        var cut = Render<NewTicket>();
        await FillFormAsync(cut);

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        _snackbar.Received(1).Add(CategoryErrors.Inactive.Message, Severity.Error, Arg.Any<Action<SnackbarOptions>>(), Arg.Any<string>());
        Services.GetRequiredService<NavigationManager>().Uri.ShouldBe("http://localhost/");
    }

    [Fact]
    public async Task Submit_ValidationResult_ShowsErrorsAtFieldsWithoutSnackbar()
    {
        // Arrange
        CreateTicketReturns(Error.Validation(
        [
            (nameof(CreateTicketCommand.Title), TicketErrors.TitleLength.Message),
            (nameof(CreateTicketCommand.Description), TicketErrors.DescriptionLength.Message),
        ]));
        var cut = Render<NewTicket>();
        await FillFormAsync(cut);

        // Act
        await ClickSubmitAsync(cut);

        // Assert
        TextField(cut, "Cím").Markup.ShouldContain(TicketErrors.TitleLength.Message);
        TextField(cut, "Leírás").Markup.ShouldContain(TicketErrors.DescriptionLength.Message);
        _snackbar.DidNotReceiveWithAnyArgs().Add(default(string)!, default, default, default);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public async Task SelectImpactAndUrgency_HighAndHigh_ShowsCriticalExpectedPriority()
    {
        // Arrange
        var cut = Render<NewTicket>();
        cut.FindAll("#expected-priority").ShouldBeEmpty();

        // Act
        await cut.InvokeAsync(() => Select<Impact?>(cut, "Hatás").Instance.SelectOption(Impact.High));
        await cut.InvokeAsync(() => Select<Urgency?>(cut, "Sürgősség").Instance.SelectOption(Urgency.High));

        // Assert
        cut.Find("#expected-priority").TextContent.ShouldContain("Várható prioritás: Kritikus");
    }

    private void CreateTicketReturns(Result<Guid> result) =>
        _runner.RunAsync(
                Arg.Any<Func<CreateTicketHandler, CancellationToken, Task<Result<Guid>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(result);

    private static async Task FillFormAsync(IRenderedComponent<NewTicket> cut)
    {
        TextField(cut, "Cím").Find("input").Change("Nem indul a laptop");
        TextField(cut, "Leírás").Find("textarea").Change("Bekapcsoláskor a laptop képernyője fekete marad.");
        await cut.InvokeAsync(() => Select<Impact?>(cut, "Hatás").Instance.SelectOption(Impact.High));
        await cut.InvokeAsync(() => Select<Urgency?>(cut, "Sürgősség").Instance.SelectOption(Urgency.Medium));
        await cut.InvokeAsync(() => Select<Guid?>(cut, "Kategória").Instance.SelectOption(Hardware.Id));
    }

    private static Task ClickSubmitAsync(IRenderedComponent<NewTicket> cut) =>
        cut.FindAll("button").Single(button => button.TextContent.Trim() == SubmitText).ClickAsync(new());

    private static IRenderedComponent<MudTextField<string>> TextField(IRenderedComponent<NewTicket> cut, string label) =>
        cut.FindComponents<MudTextField<string>>().Single(field => field.Instance.Label == label);

    private static IRenderedComponent<MudSelect<T>> Select<T>(IRenderedComponent<NewTicket> cut, string label) =>
        cut.FindComponents<MudSelect<T>>().Single(select => select.Instance.Label == label);
}
