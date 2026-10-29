using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDesk.Application.Categories.CreateCategory;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Application.Tests.Categories.CreateCategory;

// The real validator runs; only the ports are substituted.
public sealed class CreateCategoryHandlerTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RecordingLogger<CreateCategoryHandler> _logger = new();
    private Category? _addedCategory;

    public CreateCategoryHandlerTests()
    {
        _categories.Add(Arg.Do<Category>(category => _addedCategory = category));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutChanges()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutChanges()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_DuplicateCode_ReturnsValidationErrorWithoutAdd()
    {
        // Arrange
        _categories.CodeExistsAsync("PRINTER", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.Code");
        result.Error.Message.ShouldBe(CategoryErrors.CodeAlreadyExists.Message);
        result.Error.FieldErrors.Keys.ShouldBe(["Code"]);
        _categories.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_InvalidFields_ReturnsEveryFieldError()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new CreateCategoryCommand("printer", "X", new string('a', 501)), CancellationToken);

        // Assert
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Keys.ShouldBe(["Code", "Name", "Description"], ignoreOrder: true);
        _categories.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_AddsActiveCategorySavesAndLogs()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new CreateCategoryCommand(" PRINTER ", " Nyomtató ", null), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var category = _addedCategory.ShouldNotBeNull();
        result.Value.ShouldBe(category.Id);
        (category.Code, category.Name, category.Description, category.IsActive).ShouldBe(("PRINTER", "Nyomtató", string.Empty, true));
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        (entry.Properties["CategoryId"], entry.Properties["ActorUserId"]).ShouldBe((category.Id, TestUsers.UserId));
    }

    [Fact]
    public async Task HandleAsync_DuplicateSavedConcurrently_ReturnsCodeAlreadyExistsWithoutLog()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(CategoryErrors.CodeAlreadyExists));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(ValidCommand(), CancellationToken);

        // Assert
        result.Error.ShouldBe(CategoryErrors.CodeAlreadyExists);
        result.Error.Message.ShouldBe("Ezzel a kóddal már létezik kategória.");
        _logger.Entries.ShouldBeEmpty();
    }

    private static CreateCategoryCommand ValidCommand() => new("PRINTER", "Nyomtató", "Nyomtatók és szkennerek hibái.");

    private CreateCategoryHandler CreateHandler(ICurrentUser currentUser) =>
        new(currentUser, new CreateCategoryValidator(_categories), _categories, _unitOfWork, _logger);

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _categories.DidNotReceiveWithAnyArgs().CodeExistsAsync(default!, CancellationToken);
        _categories.DidNotReceiveWithAnyArgs().Add(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
