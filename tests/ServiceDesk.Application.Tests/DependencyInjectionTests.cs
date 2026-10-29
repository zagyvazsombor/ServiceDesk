using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Application.Tickets;
using ServiceDesk.Domain.Assignment;
using Shouldly;

namespace ServiceDesk.Application.Tests;

// AGENTS.md §5: conventions of AddApplication. They check every handler and validator of the Application assembly,
// so a new use case is covered without a new test.
public sealed class DependencyInjectionTests
{
    private static readonly Type[] ConcreteApplicationTypes = typeof(DependencyInjection).Assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
        .ToArray();

    [Fact]
    public void AddApplication_HandlerClasses_AreRegisteredAsScoped()
    {
        // Arrange
        var handlerTypes = ConcreteApplicationTypes.Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal));

        // Act
        var services = new ServiceCollection().AddApplication();

        // Assert
        foreach (var handlerType in handlerTypes)
        {
            services.ShouldContain(descriptor => descriptor.ServiceType == handlerType
                && descriptor.ImplementationType == handlerType
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        }
    }

    [Fact]
    public void AddApplication_Validators_AreRegisteredAsScoped()
    {
        // Arrange
        var validatorTypes = ConcreteApplicationTypes.Where(type => type.GetInterfaces().Any(IsValidatorInterface));

        // Act
        var services = new ServiceCollection().AddApplication();

        // Assert
        foreach (var validatorType in validatorTypes)
        {
            services.ShouldContain(descriptor => IsValidatorInterface(descriptor.ServiceType)
                && descriptor.ImplementationType == validatorType
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        }
    }

    [Fact]
    public void AddApplication_AssignmentServices_AreRegistered()
    {
        // Act
        var services = new ServiceCollection().AddApplication();

        // Assert
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(TicketAssignmentService)
            && descriptor.ImplementationType == typeof(TicketAssignmentService)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(IAssignmentStrategy)
            && descriptor.ImplementationType == typeof(LeastLoadedAssignmentStrategy)
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        services.ShouldContain(descriptor => descriptor.ServiceType == typeof(TimeProvider)
            && descriptor.ImplementationInstance == TimeProvider.System);
    }

    [Fact]
    public void AddApplication_TimeProviderRegisteredBefore_KeepsIt()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider();

        // Act
        var services = new ServiceCollection().AddSingleton<TimeProvider>(fakeTime).AddApplication();

        // Assert
        services.Where(descriptor => descriptor.ServiceType == typeof(TimeProvider))
            .ShouldHaveSingleItem()
            .ImplementationInstance.ShouldBeSameAs(fakeTime);
    }

    private static bool IsValidatorInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IValidator<>);
}
