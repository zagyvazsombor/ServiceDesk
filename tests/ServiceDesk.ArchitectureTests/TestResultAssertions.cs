using Shouldly;

namespace ServiceDesk.ArchitectureTests;

internal static class TestResultAssertions
{
    // Fully qualified: Xunit.TestResult is also in scope through the global using.
    public static void ShouldBeSuccessful(this NetArchTest.Rules.TestResult result) =>
        result.IsSuccessful.ShouldBeTrue(
            $"Violating types: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
