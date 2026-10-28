using ServiceDesk.Application.Common.Security;

namespace ServiceDesk.Web.UseCases;

public static class UseCaseServiceCollectionExtensions
{
    public static IServiceCollection AddUseCaseRunner(this IServiceCollection services)
    {
        // The handlers get ICurrentUser; the UseCaseRunner initializes the same scoped instance.
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(serviceProvider => serviceProvider.GetRequiredService<CurrentUser>());
        services.AddScoped<IUseCaseRunner, UseCaseRunner>();

        return services;
    }
}
