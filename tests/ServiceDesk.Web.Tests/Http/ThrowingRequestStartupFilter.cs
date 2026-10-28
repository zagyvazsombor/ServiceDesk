using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ServiceDesk.Web.Tests.Http;

// Test-only: a request handler at the end of the application pipeline that throws for one path, so the
// exception handler of Program.cs (outside Development) re-executes the request for the /Error page.
internal sealed class ThrowingRequestStartupFilter : IStartupFilter
{
    public const string Path = "/test/throw";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use((HttpContext context, RequestDelegate nextHandler) =>
            context.Request.Path == Path ? throw new InvalidOperationException("Test failure.") : nextHandler(context));
    };
}
