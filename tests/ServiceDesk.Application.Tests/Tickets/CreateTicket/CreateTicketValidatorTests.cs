using ServiceDesk.Application.Tickets.CreateTicket;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Application.Tests.Tickets.CreateTicket;

// BR-01 and the field limits of AGENTS.md §10.
public sealed class CreateTicketValidatorTests
{
    private static readonly CreateTicketCommand ValidCommand =
        new(TestData.ValidTitle, TestData.ValidDescription, Impact.Medium, Urgency.Low, Guid.CreateVersion7());

    private readonly CreateTicketValidator _validator = new();

    [Fact]
    [Trait("BR", "BR-01")]
    public void Validate_ValidCommand_HasNoErrors()
    {
        // Act
        var result = _validator.Validate(ValidCommand);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [Trait("BR", "BR-01")]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void Validate_TitleLength_FollowsLimits(int length, bool isValid)
    {
        // Act
        var result = _validator.Validate(ValidCommand with { Title = new string('a', length) });

        // Assert
        result.IsValid.ShouldBe(isValid);
    }

    [Theory]
    [Trait("BR", "BR-01")]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public void Validate_DescriptionLength_FollowsLimits(int length, bool isValid)
    {
        // Act
        var result = _validator.Validate(ValidCommand with { Description = new string('a', length) });

        // Assert
        result.IsValid.ShouldBe(isValid);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Validate_TitleTooShortAfterTrim_ReturnsTitleError()
    {
        // Act
        var result = _validator.Validate(ValidCommand with { Title = "   abcd   " });

        // Assert
        var failure = result.Errors.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe(nameof(CreateTicketCommand.Title));
        failure.ErrorMessage.ShouldBe(TicketErrors.TitleLength.Message);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Validate_NullTexts_ReturnsTitleAndDescriptionErrors()
    {
        // Act
        var result = _validator.Validate(ValidCommand with { Title = null!, Description = null! });

        // Assert
        result.Errors.Select(failure => failure.PropertyName)
            .ShouldBe([nameof(CreateTicketCommand.Title), nameof(CreateTicketCommand.Description)]);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Validate_MissingImpactAndUrgency_ReturnsBothErrors()
    {
        // Act
        var result = _validator.Validate(ValidCommand with { Impact = null, Urgency = null });

        // Assert
        result.Errors.Select(failure => (failure.PropertyName, failure.ErrorMessage)).ShouldBe(
        [
            (nameof(CreateTicketCommand.Impact), TicketErrors.ImpactInvalid.Message),
            (nameof(CreateTicketCommand.Urgency), TicketErrors.UrgencyInvalid.Message),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Validate_UndefinedImpactAndUrgency_ReturnsBothErrors()
    {
        // Act
        var result = _validator.Validate(ValidCommand with { Impact = (Impact)99, Urgency = (Urgency)99 });

        // Assert
        result.Errors.Select(failure => failure.PropertyName)
            .ShouldBe([nameof(CreateTicketCommand.Impact), nameof(CreateTicketCommand.Urgency)]);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Validate_EmptyCategoryId_ReturnsCategoryError()
    {
        // Act
        var result = _validator.Validate(ValidCommand with { CategoryId = Guid.Empty });

        // Assert
        var failure = result.Errors.ShouldHaveSingleItem();
        failure.PropertyName.ShouldBe(nameof(CreateTicketCommand.CategoryId));
        failure.ErrorMessage.ShouldBe("Válassz kategóriát.");
    }
}
