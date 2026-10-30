using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.Common.Ai;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Infrastructure.Ai;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Persistence.Queries;
using ServiceDesk.Infrastructure.Persistence.Repositories;
using ServiceDesk.Infrastructure.Persistence.Seed;

namespace ServiceDesk.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' not found.");

        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategorySuggestionRepository, CategorySuggestionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<ITicketQueries, TicketQueries>();
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<RoleSeeder>();
        services.AddScoped<DatabaseSeeder>();

        // AGENTS.md §9: a wrong Ai section stops the host at start-up; the provider is chosen from the validated
        // options when the chat client is first resolved.
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();
        services.AddChatClient(ChatClientFactory.Create);
        services.AddSingleton<ICategorySuggester, LlmCategorySuggester>();
        services.AddSingleton<ISuggestionRateLimiter, SuggestionRateLimiter>();
        // The clock of the suggestion timeout; the one of AddApplication (or of a test) is kept.
        services.TryAddSingleton(TimeProvider.System);

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // AGENTS.md §8: no e-mail infrastructure, so accounts need no confirmation (ADR-005).
                options.SignIn.RequireConfirmedAccount = false;
                // AGENTS.md §8: at least 10 characters; the other password rules keep the template defaults.
                options.Password.RequiredLength = 10;
                // AGENTS.md §8: 5 failed attempts lock the account for 15 minutes.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            // RoleManager for the seeded Reporter and Admin roles (AGENTS.md §7).
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }
}
