using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Common;

// AGENTS.md §11: a validation error carries every invalid field; Code and Message stay those of the first failure.
public sealed class ErrorTests
{
    [Fact]
    public void Constructor_NonValidationError_HasNoFieldErrors()
    {
        var error = new Error("Test.Failure", "Teszthiba.", ErrorType.BusinessRule);

        error.FieldErrors.ShouldBeEmpty();
    }

    [Fact]
    public void Validation_SeveralFields_KeepsEveryFieldAndCodeOfFirstFailure()
    {
        var error = Error.Validation(
        [
            ("Title", "Rövid cím."),
            ("Description", "Rövid leírás."),
            ("Impact", "Nincs hatás."),
        ]);

        error.Code.ShouldBe("Validation.Title");
        error.Message.ShouldBe("Rövid cím.");
        error.Type.ShouldBe(ErrorType.Validation);
        error.FieldErrors.Keys.ShouldBe(["Title", "Description", "Impact"], ignoreOrder: true);
        error.FieldErrors["Description"].ShouldBe(["Rövid leírás."]);
    }

    [Fact]
    public void Validation_SameFieldTwice_GroupsMessagesInOrder()
    {
        var error = Error.Validation([("Title", "Első."), ("Description", "Leírás."), ("Title", "Második.")]);

        error.FieldErrors.Count.ShouldBe(2);
        error.FieldErrors["Title"].ShouldBe(["Első.", "Második."]);
    }

    [Fact]
    public void Validation_NoFailures_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => Error.Validation([]));
    }

    [Fact]
    public void Validation_NullFailures_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => Error.Validation(null!));
    }

    [Fact]
    public void Equals_SameFieldErrors_AreEqual()
    {
        var first = Error.Validation([("Title", "Rövid cím."), ("Impact", "Nincs hatás.")]);
        var second = Error.Validation([("Title", "Rövid cím."), ("Impact", "Nincs hatás.")]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentFieldErrors_AreNotEqual()
    {
        var first = Error.Validation([("Title", "Rövid cím."), ("Impact", "Nincs hatás.")]);
        var second = Error.Validation([("Title", "Rövid cím."), ("Urgency", "Nincs sürgősség.")]);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Equals_ValidationErrorAndPlainErrorWithSameCode_AreNotEqual()
    {
        var withFields = Error.Validation([("Title", "Rövid cím.")]);
        var withoutFields = new Error("Validation.Title", "Rövid cím.", ErrorType.Validation);

        withFields.ShouldNotBe(withoutFields);
    }
}
