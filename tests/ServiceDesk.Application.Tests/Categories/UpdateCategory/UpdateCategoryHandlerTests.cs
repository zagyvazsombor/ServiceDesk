using NSubstitute;
using ServiceDesk.Application.Categories.UpdateCategory;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Application.Tests.Categories.UpdateCategory;

// The real validator runs; only the ports are substituted.
public sealed class UpdateCategoryHandlerTests
{
    private const string NewName = "Eszközök";
    private const string NewDescription = "Számítógépek, nyomtatók és perifériák.";

    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RecordingLogger<UpdateCategoryHandler> _logger = new();
    private readonly Category _category = TestData.CreateCategory();

    public UpdateCategoryHandlerTests()
    {
        _categories.GetByIdAsync(_category.Id, Arg.Any<CancellationToken>()).Returns(_category);
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(Command(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(Command(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        _category.Name.ShouldBe("Hardver");
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_InvalidFields_ReturnsEveryFieldErrorWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new UpdateCategoryCommand(_category.Id, " X ", new string('a', 501)), CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.Name");
        result.Error.FieldErrors.Keys.ShouldBe(["Name", "Description"], ignoreOrder: true);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_UnknownCategory_ReturnsNotFound()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new UpdateCategoryCommand(Guid.CreateVersion7(), NewName, NewDescription), CancellationToken);

        // Assert
        result.Error.ShouldBe(CategoryErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_ChangesNameAndDescriptionButNotCodeAndSaves()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new UpdateCategoryCommand(_category.Id, $" {NewName} ", NewDescription), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (_category.Code, _category.Name, _category.Description, _category.IsActive)
            .ShouldBe(("HARDWARE", NewName, NewDescription, true));
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        _logger.Entries.ShouldHaveSingleItem().Properties["CategoryId"].ShouldBe(_category.Id);
    }

    [Fact]
    public async Task HandleAsync_NullDescription_ClearsDescription()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new UpdateCategoryCommand(_category.Id, NewName, null), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _category.Description.ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_InactiveCategory_UpdatesAndKeepsItInactive()
    {
        // Arrange
        var inactive = TestData.CreateInactiveCategory();
        _categories.GetByIdAsync(inactive.Id, Arg.Any<CancellationToken>()).Returns(inactive);

        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new UpdateCategoryCommand(inactive.Id, NewName, NewDescription), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (inactive.Name, inactive.IsActive).ShouldBe((NewName, false));
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(), CancellationToken);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        _logger.Entries.ShouldBeEmpty();
    }

    private UpdateCategoryCommand Command() => new(_category.Id, NewName, NewDescription);

    private UpdateCategoryHandler CreateHandler(ICurrentUser currentUser) =>
        new(currentUser, new UpdateCategoryValidator(), _categories, _unitOfWork, _logger);

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _categories.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
