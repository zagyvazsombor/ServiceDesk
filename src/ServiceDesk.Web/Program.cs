using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using MudBlazor.Services;
using ServiceDesk.Application;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Web.Components;
using ServiceDesk.Web.Components.Account;
using ServiceDesk.Web.Security;
using ServiceDesk.Web.UseCases;

var builder = WebApplication.CreateBuilder(args);

// Add MudBlazor services
builder.Services.AddMudServices();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddUseCaseRunner();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// AGENTS.md §11: a page refused by its role is logged as a Warning; the default handler still answers.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AccessDeniedLoggingResultHandler>();

var app = builder.Build();

// ADR-008: first in the pipeline, so that every response gets the security headers.
app.UseSecurityHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // AGENTS.md §7: migrations, the roles, then the demo data.
    await app.Services.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping);
    app.UseMigrationsEndPoint();
}
else
{
    // ADR-009: the roles in every environment; no migration and no demo data outside Development.
    await app.Services.SeedRolesAsync(app.Lifetime.ApplicationStopping);
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

// AGENTS.md §6: hu-HU is the only culture; the circuit takes it over from the initial request.
app.UseRequestLocalization("hu-HU");

app.UseAntiforgery();

app.MapStaticAssets();
// ADR-008: the framework enforces its own CSP frame-ancestors (default 'self') for the interactive server endpoints;
// browsers prefer it to X-Frame-Options, so it gets the same DENY meaning.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = "'none'");

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

await app.RunAsync();
