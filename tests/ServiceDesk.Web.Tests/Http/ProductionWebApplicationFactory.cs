using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace ServiceDesk.Web.Tests.Http;

// The real Program.cs in Production on the ServiceDesk_WebTests database, with the real Identity cookie: no stubbed
// service and no test sign-in scheme.
internal sealed class ProductionWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", ProductionDatabaseFixture.ConnectionString);
    }
}
