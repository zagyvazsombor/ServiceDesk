using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Users.GetCurrentUserProfile;

namespace ServiceDesk.Web.Tests.Http;

// The real Program.cs pipeline in the Production environment (exception handler page, no database start-up),
// with the test-only sign-in scheme and a stubbed profile query, so the tests need no database.
public sealed class ServiceDeskWebApplicationFactory : WebApplicationFactory<Program>
{
    // AddInfrastructure requires a connection string; the tests never open the database.
    private const string ConnectionString = @"Server=(localdb)\MSSQLLocalDB;Database=ServiceDesk_Tests;Trusted_Connection=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options => options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, configureOptions: null);

            // The UserMenu reads the profile of the signed-in user through the use case runner.
            var userQueries = Substitute.For<IUserQueries>();
            userQueries.GetProfileAsync(TestUsers.UserId, Arg.Any<CancellationToken>())
                .Returns(new CurrentUserProfileDto(TestUsers.UserId, TestUsers.Email, [Roles.Reporter], Agent: null));
            services.RemoveAll<IUserQueries>();
            services.AddSingleton(userQueries);

            services.AddSingleton<IStartupFilter, ThrowingRequestStartupFilter>();
        });
    }
}
