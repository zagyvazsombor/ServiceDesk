using FluentValidation;
using ServiceDesk.Application.Common.Validation;
using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Application.Tests.Common.Validation;

// AGENTS.md §11: every FluentValidation failure reaches the Result; the Code stays that of the first failure.
public sealed class ValidatorExtensionsTests
{
    private readonly SampleValidator _validator = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ValidateToResultAsync_ValidInstance_ReturnsSuccess()
    {
        // Act
        var result = await _validator.ValidateToResultAsync(new Sample("Teszt", 1), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateToResultAsync_SeveralInvalidFields_ReturnsEveryFieldError()
    {
        // Act
        var result = await _validator.ValidateToResultAsync(new Sample(string.Empty, 0), CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.Name");
        result.Error.Message.ShouldBe("Adj meg nevet.");
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.Count.ShouldBe(2);
        result.Error.FieldErrors["Name"].ShouldBe(["Adj meg nevet.", "A név legalább 3 karakter."]);
        result.Error.FieldErrors["Count"].ShouldBe(["A darabszám legyen pozitív."]);
    }

    [Fact]
    public async Task ValidateToResultAsync_OnlySecondFieldInvalid_UsesItsCode()
    {
        // Act
        var result = await _validator.ValidateToResultAsync(new Sample("Teszt", 0), CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.Count");
        result.Error.FieldErrors.Keys.ShouldBe(["Count"]);
    }

    private sealed record Sample(string Name, int Count);

    private sealed class SampleValidator : AbstractValidator<Sample>
    {
        public SampleValidator()
        {
            RuleFor(sample => sample.Name)
                .NotEmpty().WithMessage("Adj meg nevet.")
                .MinimumLength(3).WithMessage("A név legalább 3 karakter.");
            RuleFor(sample => sample.Count)
                .GreaterThan(0).WithMessage("A darabszám legyen pozitív.");
        }
    }
}
