using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceDesk.Application;

public static class DependencyInjection
{
    private const string HandlerSuffix = "Handler";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(DependencyInjection).Assembly;

        // AGENTS.md §5: every *Handler class is registered as scoped, so that one use case gets one scope.
        var handlerTypes = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && type.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal));

        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);
        }

        // AGENTS.md §5: the validators live in the same scope as the handler that uses them.
        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped);

        return services;
    }
}
