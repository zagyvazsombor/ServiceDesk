using NSubstitute;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.GetActiveCategories;

// Any signed-in user may read the categories, so there is no "wrong role" and no "foreign resource" case.
public sealed class GetActiveCategoriesHandlerTests
{
    private readonly ITicketQueries _ticketQueries = Substitute.For<ITicketQueries>();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutQuery()
    {
        // Act
        var result = await new GetActiveCategoriesHandler(TestUsers.Anonymous(), _ticketQueries)
            .HandleAsync(new GetActiveCategoriesQuery(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await _ticketQueries.DidNotReceiveWithAnyArgs().GetActiveCategoriesAsync(CancellationToken);
    }

    [Theory]
    [Trait("BR", "BR-13")]
    [InlineData(Roles.Reporter)]
    [InlineData(Roles.Admin)]
    public async Task HandleAsync_SignedInUser_ReturnsActiveCategories(string role)
    {
        // Arrange
        IReadOnlyList<ActiveCategoryDto> categories =
            [new ActiveCategoryDto(Guid.CreateVersion7(), "HARDWARE", "Hardver", "Számítógépek és perifériák.")];
        _ticketQueries.GetActiveCategoriesAsync(Arg.Any<CancellationToken>()).Returns(categories);

        // Act
        var result = await new GetActiveCategoriesHandler(TestUsers.SignedIn(role), _ticketQueries)
            .HandleAsync(new GetActiveCategoriesQuery(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeSameAs(categories);
    }
}
