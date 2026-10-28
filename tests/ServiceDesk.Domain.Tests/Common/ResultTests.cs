using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Common;

public sealed class ResultTests
{
    private static readonly Error SampleError = new("Test.Failure", "Teszthiba.", ErrorType.BusinessRule);

    [Fact]
    public void Success_NoArguments_IsSuccessWithoutError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void Failure_WithError_IsFailureWithError()
    {
        var result = Result.Failure(SampleError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
    }

    [Fact]
    public void Failure_NullError_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void SuccessOfT_WithValue_ExposesValue()
    {
        var result = Result.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void FailureOfT_WithError_ThrowsOnValueAccess()
    {
        var result = Result.Failure<int>(SampleError);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        Result result = SampleError;
        Result<int> resultOfT = SampleError;

        result.Error.ShouldBe(SampleError);
        resultOfT.Error.ShouldBe(SampleError);
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        Result<string> result = "érték";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("érték");
    }
}
