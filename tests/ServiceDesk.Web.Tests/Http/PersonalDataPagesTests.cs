using System.Net;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Infrastructure.Persistence;
using Shouldly;

namespace ServiceDesk.Web.Tests.Http;

// P-9A, AGENTS.md §6 exception: there is no self-service account deletion. The personal data page keeps the download
// but has no Delete link; the deletion page only gives a notice. A signed-in user of the real Identity cookie.
[Collection(nameof(ProductionDatabaseCollection))]
public sealed class PersonalDataPagesTests(ProductionDatabaseFixture database)
{
    private const string DeletePath = "/Account/Manage/DeletePersonalData";
    private const string Notice = "A fiók törlése nem érhető el, fordulj az adminisztrátorhoz.";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetPersonalData_SignedInUser_KeepsDownloadAndMenuWithoutDeleteLink()
    {
        // Arrange
        await using var factory = new ProductionWebApplicationFactory();
        using var client = await SignedInClientAsync(factory, AccountForms.NewEmail());

        // Act
        using var response = await client.GetAsync("/Account/Manage/PersonalData", CancellationToken);
        var document = new HtmlParser().ParseDocument(await response.Content.ReadAsStringAsync(CancellationToken));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        document.QuerySelector("form[action='Account/Manage/DownloadPersonalData']").ShouldNotBeNull();
        document.QuerySelector("a[href='Account/Manage/PersonalData']").ShouldNotBeNull(); // the menu item stays
        document.QuerySelectorAll("a[href]")
            .ShouldNotContain(link => link.GetAttribute("href")!.Contains("DeletePersonalData", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetDeletePersonalData_SignedInUser_ShowsNoticeWithoutForm()
    {
        // Arrange
        await using var factory = new ProductionWebApplicationFactory();
        using var client = await SignedInClientAsync(factory, AccountForms.NewEmail());

        // Act
        using var response = await client.GetAsync(DeletePath, CancellationToken);
        var html = await response.Content.ReadAsStringAsync(CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain(Notice);
        AccountForms.FormFields(html, "delete-user").ShouldBeNull();
        new HtmlParser().ParseDocument(html).QuerySelector("input[type='password']").ShouldBeNull();
    }

    [Fact]
    public async Task PostDeletePersonalData_FormOfTheTemplate_IsRefusedAndKeepsTheUser()
    {
        // Arrange: the request the former form of the template sent, with a valid antiforgery token.
        await using var factory = new ProductionWebApplicationFactory();
        var email = AccountForms.NewEmail();
        using var client = await SignedInClientAsync(factory, email);
        using var page = await client.GetAsync("/Account/Manage/PersonalData", CancellationToken);
        var token = new HtmlParser().ParseDocument(await page.Content.ReadAsStringAsync(CancellationToken))
            .QuerySelector("input[name='__RequestVerificationToken']")
            .ShouldNotBeNull()
            .GetAttribute("value")!;
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["_handler"] = "delete-user",
            ["Input.Password"] = AccountForms.Password,
        });

        // Act
        using var response = await client.PostAsync(DeletePath, form, CancellationToken);

        // Assert: the page has no form of that name, so nothing handles the request.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var scope = database.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.Users.AnyAsync(user => user.Email == email, CancellationToken)).ShouldBeTrue();
    }

    private static async Task<HttpClient> SignedInClientAsync(ProductionWebApplicationFactory factory, string email)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var registration = await AccountForms.RegisterAsync(client, email, CancellationToken);
        registration.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        return client;
    }
}
