using Microsoft.Extensions.Logging;
using NSubstitute;
using ServiceDesk.Application.Categories.ActivateCategory;
using ServiceDesk.Application.Categories.DeactivateCategory;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Application.Tests.Categories;

// DeactivateCategory and ActivateCategory differ only in the direction of the change (BR-13).
public sealed class CategoryActivationHandlersTests
{
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public CategoryActivationHandlersTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    public enum Operation
    {
        Deactivate,
        Activate,
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(Operation.Deactivate)]
    [InlineData(Operation.Activate)]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading(Operation operation)
    {
        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Anonymous(), Guid.CreateVersion7());

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Theory]
    [InlineData(Operation.Deactivate)]
    [InlineData(Operation.Activate)]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading(Operation operation)
    {
        // Arrange
        var category = CategoryCanBeLoaded(CategoryBefore(operation));
        var isActive = category.IsActive;

        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Reporter(), category.Id);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        category.IsActive.ShouldBe(isActive);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Theory]
    [InlineData(Operation.Deactivate)]
    [InlineData(Operation.Activate)]
    public async Task HandleAsync_UnknownCategory_ReturnsNotFound(Operation operation)
    {
        // Act
        var (result, _) = await RunAsync(operation, TestUsers.Admin(), Guid.CreateVersion7());

        // Assert
        result.Error.ShouldBe(CategoryErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Theory]
    [InlineData(Operation.Deactivate, false)]
    [InlineData(Operation.Activate, true)]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_CategoryInOtherState_ChangesStateSavesAndLogs(Operation operation, bool isActiveAfter)
    {
        // Arrange
        var category = CategoryCanBeLoaded(CategoryBefore(operation));

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), category.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        category.IsActive.ShouldBe(isActiveAfter);
        (category.Code, category.Name).ShouldBe(("HARDWARE", "Hardver"));
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        var entry = log.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Information);
        (entry.Properties["CategoryId"], entry.Properties["ActorUserId"]).ShouldBe((category.Id, TestUsers.UserId));
    }

    [Theory]
    [InlineData(Operation.Deactivate, false)]
    [InlineData(Operation.Activate, true)]
    public async Task HandleAsync_CategoryAlreadyInTargetState_ReturnsSuccessWithoutSaveOrLog(Operation operation, bool isActive)
    {
        // Arrange
        var category = CategoryCanBeLoaded(isActive ? TestData.CreateCategory() : TestData.CreateInactiveCategory());

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), category.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        category.IsActive.ShouldBe(isActive);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
        log.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Operation.Deactivate)]
    [InlineData(Operation.Activate)]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog(Operation operation)
    {
        // Arrange
        var category = CategoryCanBeLoaded(CategoryBefore(operation));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var (result, log) = await RunAsync(operation, TestUsers.Admin(), category.Id);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        log.ShouldBeEmpty();
    }

    // The deactivation starts from an active category, the activation from an inactive one; both are HARDWARE.
    private static Category CategoryBefore(Operation operation)
    {
        var category = TestData.CreateCategory();
        if (operation == Operation.Activate)
        {
            category.Deactivate();
        }

        return category;
    }

    private static async Task<(Result Result, IReadOnlyList<LogEntry> Log)> RunWithLoggerAsync<THandler>(
        Func<RecordingLogger<THandler>, Task<Result>> run)
    {
        var logger = new RecordingLogger<THandler>();
        var result = await run(logger);
        return (result, logger.Entries);
    }

    private Task<(Result Result, IReadOnlyList<LogEntry> Log)> RunAsync(
        Operation operation,
        ICurrentUser currentUser,
        Guid categoryId) => operation switch
        {
            Operation.Deactivate => RunWithLoggerAsync<DeactivateCategoryHandler>(logger =>
                new DeactivateCategoryHandler(currentUser, _categories, _unitOfWork, logger)
                    .HandleAsync(new DeactivateCategoryCommand(categoryId), CancellationToken)),
            Operation.Activate => RunWithLoggerAsync<ActivateCategoryHandler>(logger =>
                new ActivateCategoryHandler(currentUser, _categories, _unitOfWork, logger)
                    .HandleAsync(new ActivateCategoryCommand(categoryId), CancellationToken)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
        };

    private Category CategoryCanBeLoaded(Category category)
    {
        _categories.GetByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
        return category;
    }

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _categories.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
