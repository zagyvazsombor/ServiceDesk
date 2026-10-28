using System.Net;
using AngleSharp.Html.Parser;
using Shouldly;

namespace ServiceDesk.Web.Tests.Http;

// ADR-006: a re-executed status code page (404) is rendered statically, so its logout form carries the antiforgery
// token itself. The exception handler page is rendered statically by the framework; the error page cases guard that.
public sealed class LogoutFormTests(ServiceDeskWebApplicationFactory factory) : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private const string UnknownPath = "/nincs-ilyen-oldal";
    private const string TokenField = "__RequestVerificationToken";
    private const string IdentityCookie = ".AspNetCore.Identity.Application";

    // An interactive root component leaves a marker comment; the state the circuit starts from is another one.
    private const string InteractiveMarker = "<!--Blazor:{";
    private const string PersistedState = "<!--Blazor-Server-Component-State:";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(UnknownPath, HttpStatusCode.NotFound)]
    [InlineData(ThrowingRequestStartupFilter.Path, HttpStatusCode.InternalServerError)]
    public async Task Get_StatusCodeOrErrorPage_RendersStaticallyWithLogoutToken(string path, HttpStatusCode expectedStatus)
    {
        // Arrange
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync(path, CancellationToken);
        var html = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(expectedStatus);
        html.ShouldNotContain(InteractiveMarker);
        var fields = LogoutFormFields(html).ShouldNotBeNull();
        fields.ShouldContainKey(TokenField);
        fields[TokenField].ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(UnknownPath)]
    [InlineData(ThrowingRequestStartupFilter.Path)]
    public async Task PostLogout_TokenOfStatusCodeOrErrorPage_SignsOutAndRedirectsBack(string path)
    {
        // Arrange
        using var client = CreateClient();
        using var page = await client.GetAsync(path, CancellationToken);
        var fields = LogoutFormFields(await page.Content.ReadAsStringAsync(CancellationToken)).ShouldNotBeNull();
        using var form = new FormUrlEncodedContent(fields);

        // Act
        using var response = await client.PostAsync("/Account/Logout", form, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe("/" + fields["ReturnUrl"]);
        response.Headers.GetValues("Set-Cookie").ShouldContain(
            cookie => cookie.StartsWith(IdentityCookie + "=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_NormalPage_StaysInteractiveWithPersistedState()
    {
        // Arrange
        using var client = CreateClient();

        // Act
        using var response = await client.GetAsync("/tickets/my", CancellationToken);
        var html = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain(InteractiveMarker);
        html.ShouldContain(PersistedState);
    }

    // Cookies are kept between requests (the antiforgery cookie of the page goes with the logout POST).
    private HttpClient CreateClient() => factory.CreateClient(new() { AllowAutoRedirect = false });

    private static Dictionary<string, string>? LogoutFormFields(string html) =>
        new HtmlParser().ParseDocument(html)
            .QuerySelector("form[action='Account/Logout']")?
            .QuerySelectorAll("input[name]")
            .ToDictionary(input => input.GetAttribute("name")!, input => input.GetAttribute("value") ?? string.Empty);
}
