using NetArchTest.Rules;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Web.UseCases;
using Shouldly;

namespace ServiceDesk.ArchitectureTests;

// AGENTS.md §4: dependency rules between the solution projects.
public sealed class DependencyRuleTests
{
    private const string ApplicationNamespace = "ServiceDesk.Application";
    private const string InfrastructureNamespace = "ServiceDesk.Infrastructure";
    private const string WebNamespace = "ServiceDesk.Web";
    private const string AccountNamespace = "ServiceDesk.Web.Components.Account";
    private const string PagesNamespace = "ServiceDesk.Web.Components.Pages";
    private const string EfCoreNamespace = "Microsoft.EntityFrameworkCore";

    [Fact]
    public void Domain_AllTypes_DoNotDependOnOtherSolutionProjects()
    {
        var result = Types.InAssembly(SolutionAssemblies.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, WebNamespace)
            .GetResult();

        result.ShouldBeSuccessful();
    }

    [Fact]
    public void Domain_Assembly_ReferencesOnlySharedFrameworkAssemblies()
    {
        // NetArchTest's OnlyHaveDependenciesOn also rejects compiler-generated types (e.g. <PrivateImplementationDetails>),
        // so the "no external package" rule is checked on the assembly references instead.
        var sharedFrameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var externalReferences = SolutionAssemblies.Domain.GetReferencedAssemblies()
            .Where(reference => !File.Exists(Path.Combine(sharedFrameworkDirectory, reference.Name + ".dll")))
            .Select(reference => reference.FullName)
            .ToList();

        externalReferences.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(EfCoreNamespace)]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("MudBlazor")]
    [InlineData("Microsoft.Extensions.AI")]
    [InlineData(InfrastructureNamespace)]
    [InlineData(WebNamespace)]
    public void Application_AllTypes_DoNotDependOnForbiddenNamespace(string forbiddenNamespace)
    {
        var result = Types.InAssembly(SolutionAssemblies.Application)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        result.ShouldBeSuccessful();
    }

    [Fact]
    public void Infrastructure_AllTypes_DoNotDependOnWeb()
    {
        var result = Types.InAssembly(SolutionAssemblies.Infrastructure)
            .ShouldNot()
            .HaveDependencyOn(WebNamespace)
            .GetResult();

        result.ShouldBeSuccessful();
    }

    [Fact]
    public void Web_TypesOutsideProgramAndAccount_DoNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(SolutionAssemblies.Web)
            .That()
            .DoNotResideInNamespaceStartingWith(AccountNamespace)
            .And()
            .DoNotHaveName("Program")
            .And()
            // Template class in the Components/Account folder, declared in the Microsoft.AspNetCore.Routing namespace.
            .DoNotHaveName("IdentityComponentsEndpointRouteBuilderExtensions")
            .ShouldNot()
            .HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        result.ShouldBeSuccessful();
    }

    [Theory]
    [InlineData(InfrastructureNamespace)]
    [InlineData(EfCoreNamespace)]
    public void WebPages_AllTypes_DoNotDependOnForbiddenNamespace(string forbiddenNamespace)
    {
        var result = Types.InAssembly(SolutionAssemblies.Web)
            .That()
            .ResideInNamespaceStartingWith(PagesNamespace)
            .ShouldNot()
            .HaveDependencyOn(forbiddenNamespace)
            .GetResult();

        result.ShouldBeSuccessful();
    }

    // AGENTS.md §5, §7: a page gets the user and the DbContext only inside a use case, through the IUseCaseRunner.
    [Theory]
    [InlineData(typeof(CurrentUser))]
    [InlineData(typeof(AppDbContext))]
    public void WebPages_AllTypes_DoNotDependOnForbiddenType(Type forbiddenType)
    {
        var result = Types.InAssembly(SolutionAssemblies.Web)
            .That()
            .ResideInNamespaceStartingWith(PagesNamespace)
            .ShouldNot()
            .HaveDependencyOn(forbiddenType.FullName)
            .GetResult();

        result.ShouldBeSuccessful();
    }
}
