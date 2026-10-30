using AngleSharp.Html.Parser;
using Shouldly;

namespace ServiceDesk.Web.Tests.Http;

// The static SSR forms of the Account pages, filled in as a browser would: the antiforgery token, the form name
// (_handler) and the inputs come from the page.
internal static class AccountForms
{
    // Test value only; it satisfies the password rules of AGENTS.md §8.
    public const string Password = "Teszt-Jelszo-2026";

    public static string NewEmail() => $"uj.bejelento.{Guid.NewGuid():N}@servicedesk.local";

    // Registers a new user; the client keeps the Identity cookie, so it is signed in afterwards.
    public static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, CancellationToken ct)
    {
        using var page = await client.GetAsync("/Account/Register", ct);
        var fields = FormFields(await page.Content.ReadAsStringAsync(ct), "register").ShouldNotBeNull();
        fields["Input.Email"] = email;
        fields["Input.Password"] = Password;
        fields["Input.ConfirmPassword"] = Password;
        using var form = new FormUrlEncodedContent(fields);
        return await client.PostAsync("/Account/Register", form, ct);
    }

    // null when the page has no form of that name.
    public static Dictionary<string, string>? FormFields(string html, string formName) =>
        new HtmlParser().ParseDocument(html)
            .QuerySelector($"input[name='_handler'][value='{formName}']")?
            .Closest("form")?
            .QuerySelectorAll("input[name]")
            .ToDictionary(input => input.GetAttribute("name")!, input => input.GetAttribute("value") ?? string.Empty);
}
