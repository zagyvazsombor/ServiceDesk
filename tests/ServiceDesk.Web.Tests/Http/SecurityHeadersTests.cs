using System.Net;
using System.Security.Cryptography;
using System.Text;
using AngleSharp.Html.Parser;
using Shouldly;

namespace ServiceDesk.Web.Tests.Http;

// ADR-008: the security headers on every kind of response, and a report-only CSP whose script hash matches the import
// map the page writes.
public sealed class SecurityHeadersTests(ServiceDeskWebApplicationFactory factory) : IClassFixture<ServiceDeskWebApplicationFactory>
{
    private const string ReportOnlyHeader = "Content-Security-Policy-Report-Only";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/tickets/my", HttpStatusCode.OK)] // interactive page; its persisted state carries an antiforgery token
    [InlineData("/Account/Login", HttpStatusCode.OK)] // static SSR form with an antiforgery token
    [InlineData("/nincs-ilyen-oldal", HttpStatusCode.NotFound)] // re-executed status code page
    [InlineData(ThrowingRequestStartupFilter.Path, HttpStatusCode.InternalServerError)] // exception handler page
    [InlineData("/favicon.ico", HttpStatusCode.OK)] // static asset
    public async Task Get_AnyResponse_HasEverySecurityHeaderOnce(string path, HttpStatusCode expectedStatus)
    {
        // Arrange
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        // Act
        using var response = await client.GetAsync(path, CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(expectedStatus);
        response.Headers.GetValues("X-Content-Type-Options").ShouldHaveSingleItem().ShouldBe("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldHaveSingleItem().ShouldBe("DENY"); // not the SAMEORIGIN of the antiforgery
        response.Headers.GetValues("Referrer-Policy").ShouldHaveSingleItem().ShouldBe("strict-origin-when-cross-origin");
        response.Headers.GetValues(ReportOnlyHeader).ShouldHaveSingleItem().ShouldContain("default-src 'self'");

        // The only enforced policy is the frame-ancestors of the framework, with the meaning of X-Frame-Options: DENY.
        if (response.Headers.TryGetValues("Content-Security-Policy", out var enforced))
        {
            enforced.ShouldHaveSingleItem().ShouldBe("frame-ancestors 'none'");
        }
    }

    [Theory]
    [InlineData("/tickets/my", HttpStatusCode.OK)]
    [InlineData("/Account/Login", HttpStatusCode.OK)]
    [InlineData("/nincs-ilyen-oldal", HttpStatusCode.NotFound)]
    [InlineData(ThrowingRequestStartupFilter.Path, HttpStatusCode.InternalServerError)]
    public async Task Get_Page_ScriptSourceAllowsOnlySelfAndTheImportMapOfThePage(string path, HttpStatusCode expectedStatus)
    {
        // Arrange
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        // Act
        using var response = await client.GetAsync(path, CancellationToken);
        var html = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(expectedStatus);
        var importMap = new HtmlParser().ParseDocument(html)
            .QuerySelectorAll("script[type='importmap']")
            .ShouldHaveSingleItem()
            .TextContent;
        importMap.ShouldNotBeNullOrWhiteSpace();
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(importMap)));
        Directive(response, "script-src").ShouldBe($"script-src 'self' 'sha256-{hash}'");
    }

    [Fact]
    public async Task Get_StaticAsset_ScriptSourceAllowsOnlySelf()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/favicon.ico", CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Directive(response, "script-src").ShouldBe("script-src 'self'");
    }

    [Fact]
    public async Task Get_Page_PolicyAllowsTheSourcesOfMudBlazorAndTheCircuit()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/tickets/my", CancellationToken);

        // Assert
        Directive(response, "style-src").ShouldBe("style-src 'self' 'unsafe-inline' https://fonts.googleapis.com");
        Directive(response, "font-src").ShouldBe("font-src 'self' https://fonts.gstatic.com");
        Directive(response, "connect-src").ShouldBe("connect-src 'self'");
        Directive(response, "frame-ancestors").ShouldBe("frame-ancestors 'none'");
        Directive(response, "object-src").ShouldBe("object-src 'none'");
    }

    [Fact]
    public async Task Get_InteractivePage_FrameworkFrameAncestorsPolicyIsNone()
    {
        // Arrange
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/tickets/my", CancellationToken);

        // Assert: browsers prefer the CSP frame-ancestors to X-Frame-Options, so it must not allow 'self'.
        response.Headers.GetValues("Content-Security-Policy").ShouldHaveSingleItem().ShouldBe("frame-ancestors 'none'");
    }

    private static string Directive(HttpResponseMessage response, string name) =>
        response.Headers.GetValues(ReportOnlyHeader).ShouldHaveSingleItem()
            .Split(';', StringSplitOptions.TrimEntries)
            .Where(directive => directive.StartsWith(name + " ", StringComparison.Ordinal))
            .ShouldHaveSingleItem();
}
