using NSubstitute;
using ServiceDesk.Application.Categories.CreateCategory;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Categories;
using Shouldly;

namespace ServiceDesk.Application.Tests.Categories.CreateCategory;

// The field limits of AGENTS.md §10 and the unique code. The validator has an async rule, so it is run with ValidateAsync.
public sealed class CreateCategoryValidatorTests
{
    private static readonly CreateCategoryCommand ValidCommand =
        new("PRINTER", "Nyomtató", "Nyomtatók és szkennerek hibái.");

    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly CreateCategoryValidator _validator;

    public CreateCategoryValidatorTests()
    {
        _validator = new CreateCategoryValidator(_categories);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ValidateAsync_ValidCommandWithNewCode_HasNoErrors()
    {
        // Act
        var result = await _validator.ValidateAsync(ValidCommand, CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ExistingCode_ReturnsCodeAlreadyExistsOnCode()
    {
        // Arrange
        _categories.CodeExistsAsync("PRINTER", Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _validator.ValidateAsync(ValidCommand with { Code = " PRINTER " }, CancellationToken);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        (error.PropertyName, error.ErrorMessage).ShouldBe(("Code", CategoryErrors.CodeAlreadyExists.Message));
    }

    [Theory]
    [InlineData("")]
    [InlineData("P")]
    [InlineData("printer")]
    [InlineData("PRINTER-1")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ_ABCD")]
    public async Task ValidateAsync_InvalidCode_ReturnsCodeInvalidWithoutLookup(string code)
    {
        // Act
        var result = await _validator.ValidateAsync(ValidCommand with { Code = code }, CancellationToken);

        // Assert
        var error = result.Errors.ShouldHaveSingleItem();
        (error.PropertyName, error.ErrorMessage).ShouldBe(("Code", CategoryErrors.CodeInvalid.Message));
        await _categories.DidNotReceiveWithAnyArgs().CodeExistsAsync(default!, CancellationToken);
    }

    [Fact]
    public async Task ValidateAsync_NullCode_ReturnsCodeInvalidWithoutLookup()
    {
        // Act
        var result = await _validator.ValidateAsync(ValidCommand with { Code = null! }, CancellationToken);

        // Assert
        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Code");
        await _categories.DidNotReceiveWithAnyArgs().CodeExistsAsync(default!, CancellationToken);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task ValidateAsync_NameLength_FollowsLimits(int length, bool isValid)
    {
        // Act
        var result = await _validator.ValidateAsync(ValidCommand with { Name = new string('a', length) }, CancellationToken);

        // Assert
        result.IsValid.ShouldBe(isValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public async Task ValidateAsync_DescriptionLength_FollowsLimit(int length, bool isValid)
    {
        // Act
        var result = await _validator.ValidateAsync(
            ValidCommand with { Description = new string('a', length) },
            CancellationToken);

        // Assert
        result.IsValid.ShouldBe(isValid);
    }

    [Fact]
    public async Task ValidateAsync_NullDescription_HasNoErrors()
    {
        // Act
        var result = await _validator.ValidateAsync(ValidCommand with { Description = null }, CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
    }
}
