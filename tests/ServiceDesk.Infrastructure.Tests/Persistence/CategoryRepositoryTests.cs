using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

public sealed class CategoryRepositoryTests(DatabaseFixture database) : IntegrationTest(database)
{
    [Fact]
    public async Task GetByIdsAsync_KnownAndUnknownIds_ReturnsOnlyKnownCategories()
    {
        // Arrange
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var software = new CategoryBuilder().WithCode("SOFTWARE").Build();
        software.Deactivate();
        await SaveAsync(hardware, network, software);

        // Act
        await using var scope = Database.CreateScope();
        var categories = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>()
            .GetByIdsAsync([hardware.Id, software.Id, Guid.CreateVersion7()], CancellationToken);

        // Assert
        categories.Select(category => (category.Code, category.IsActive))
            .ShouldBe([("HARDWARE", true), ("SOFTWARE", false)], ignoreOrder: true);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task GetActiveAsync_ActiveAndInactiveCategories_ReturnsOnlyActiveOnesByCode()
    {
        // Arrange
        var software = new CategoryBuilder().WithCode("SOFTWARE").Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var other = new CategoryBuilder().WithCode("OTHER").Build();
        other.Deactivate();
        await SaveAsync(software, hardware, other);

        // Act
        await using var scope = Database.CreateScope();
        var categories = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>()
            .GetActiveAsync(CancellationToken);

        // Assert
        categories.Select(category => category.Code).ShouldBe(["HARDWARE", "SOFTWARE"]);
    }

    [Theory]
    [InlineData("HARDWARE", true)]
    [InlineData("OTHER", true)]
    [InlineData("NETWORK", false)]
    public async Task CodeExistsAsync_ActiveAndInactiveCategories_FindsEveryExistingCode(string code, bool exists)
    {
        // Arrange
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var other = new CategoryBuilder().WithCode("OTHER").Build();
        other.Deactivate();
        await SaveAsync(hardware, other);

        // Act
        await using var scope = Database.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ICategoryRepository>()
            .CodeExistsAsync(code, CancellationToken);

        // Assert
        result.ShouldBe(exists);
    }
}
