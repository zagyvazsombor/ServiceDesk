using System.Reflection;

namespace ServiceDesk.ArchitectureTests;

internal static class SolutionAssemblies
{
    public static Assembly Domain { get; } = Assembly.Load("ServiceDesk.Domain");

    public static Assembly Application { get; } = Assembly.Load("ServiceDesk.Application");

    public static Assembly Infrastructure { get; } = Assembly.Load("ServiceDesk.Infrastructure");

    public static Assembly Web { get; } = Assembly.Load("ServiceDesk.Web");
}
