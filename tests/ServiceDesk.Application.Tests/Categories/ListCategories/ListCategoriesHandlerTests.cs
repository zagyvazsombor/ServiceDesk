using NSubstitute;
using ServiceDesk.Application.Categories.ListCategories;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using Shouldly;

namespace ServiceDesk.Application.Tests.Categories.ListCategories;

public sealed class ListCategoriesHandlerTests
{
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(new ListCategoriesQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetCategoriesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(new ListCategoriesQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetCategoriesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_Admin_ReturnsCategoriesWithInactiveOnes()
    {
        // Arrange
        IReadOnlyList<CategoryListItemDto> categories =
        [
            new(Guid.CreateVersion7(), "OTHER", "Egyéb", "Minden más.", IsActive: false),
            new(Guid.CreateVersion7(), "HARDWARE", "Hardver", "Eszközök.", IsActive: true),
        ];
        _ticketQueries.GetCategoriesAsync(Arg.Any<CancellationToken>()).Returns(categories);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(new ListCategoriesQuery(), CancellationToken);

        // Assert
        result.Value.ShouldBeSameAs(categories);
        await _ticketQueries.Received(1).GetCategoriesAsync(CancellationToken);
    }

    private ListCategoriesHandler CreateHandler(ICurrentUser currentUser) => new(currentUser, _ticketQueries);
}
