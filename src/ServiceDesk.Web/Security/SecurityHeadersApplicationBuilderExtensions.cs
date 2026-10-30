namespace ServiceDesk.Web.Security;

public static class SecurityHeadersApplicationBuilderExtensions
{
    // ADR-008: first in the pipeline, so that every response gets the headers (error pages and static files too).
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
