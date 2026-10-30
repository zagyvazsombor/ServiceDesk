using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace ServiceDesk.Web.Security;

// ADR-008: the security headers of every response. They are set when the response starts, so they also survive the
// exception handler (it clears the headers before the /Error page) and replace the X-Frame-Options: SAMEORIGIN that
// the antiforgery writes for a page with a token.
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ReferrerPolicyHeader = "Referrer-Policy";

    private static readonly string PolicyWithoutImportMap = BuildPolicy("'self'");

    // One policy per import map definition; the definition of the endpoints is built once per application start.
    private static readonly ConditionalWeakTable<ImportMapDefinition, string> PoliciesByImportMap = [];

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(
            static state =>
            {
                var httpContext = (HttpContext)state;
                var headers = httpContext.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers[ReferrerPolicyHeader] = "strict-origin-when-cross-origin";
                headers.ContentSecurityPolicyReportOnly = PolicyFor(httpContext.GetEndpoint());
                return Task.CompletedTask;
            },
            context);

        return next(context);
    }

    // The <ImportMap /> of App.razor writes the import map of the endpoint as an inline script. Its hash is computed at
    // run time from the same definition, as the fingerprinted asset URLs in it change from build to build. When the
    // response starts, the endpoint is the one that renders the page, a re-executed /Error or /not-found page included.
    private static string PolicyFor(Endpoint? endpoint)
    {
        var importMap = endpoint?.Metadata.GetMetadata<ImportMapDefinition>();
        return importMap is null
            ? PolicyWithoutImportMap
            : PoliciesByImportMap.GetValue(
                importMap,
                static definition => BuildPolicy($"'self' 'sha256-{ScriptHash(definition.ToString())}'")); // the JSON
    }

    // The browser hashes the text of the parsed script element, and HTML parsing turns CR LF and CR into LF; the JSON is
    // indented with Environment.NewLine, which is CR LF on Windows.
    private static string ScriptHash(string script) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'))));

    // ADR-008: report-only, the violations show up in the browser console. 'unsafe-inline' styles: MudBlazor renders a
    // <style> element and style attributes. connect-src 'self' covers the WebSocket of the circuit.
    private static string BuildPolicy(string scriptSources) =>
        "default-src 'self'; " +
        $"script-src {scriptSources}; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";
}
