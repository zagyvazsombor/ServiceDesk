using Microsoft.Extensions.DependencyInjection;

namespace ServiceDesk.Application;

public static class DependencyInjection
{
    private const string HandlerSuffix = "Handler";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // AGENTS.md §5: every *Handler class is registered as scoped, so that one use case gets one scope.
        var handlerTypes = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && type.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal));

        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);
        }

        return services;
    }
}
