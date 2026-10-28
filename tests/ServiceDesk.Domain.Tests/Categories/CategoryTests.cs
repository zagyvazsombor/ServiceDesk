using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Categories;

public sealed class CategoryTests
{
    private const string ValidName = "Hardver";
    private const string ValidDescription = "Számítógépek és perifériák.";

    [Fact]
    public void Create_ValidInput_ReturnsActiveCategoryWithTrimmedValues()
    {
        var result = Category.Create("  HARDWARE ", " Hardver ", " Számítógépek és perifériák. ");

        result.IsSuccess.ShouldBeTrue();
        var category = result.Value;
        category.Id.ShouldNotBe(Guid.Empty);
        category.Code.ShouldBe("HARDWARE");
        category.Name.ShouldBe("Hardver");
        category.Description.ShouldBe("Számítógépek és perifériák.");
        category.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("ACCESS_CONTROL")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ_ABC")]
    public void Create_ValidCode_Succeeds(string code)
    {
        var result = Category.Create(code, ValidName, ValidDescription);

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ_ABCD")]
    [InlineData("hardware")]
    [InlineData("HARD-WARE")]
    [InlineData("HARD WARE")]
    [InlineData("HARDWARE1")]
    [InlineData("ÁRAM")]
    public void Create_InvalidCode_ReturnsCodeInvalidError(string code)
    {
        var result = Category.Create(code, ValidName, ValidDescription);

        result.Error.ShouldBe(CategoryErrors.CodeInvalid);
    }

    [Theory]
    [InlineData(Category.NameMinLength)]
    [InlineData(Category.NameMaxLength)]
    public void Create_NameLengthAtLimit_Succeeds(int length)
    {
        var result = Category.Create("HARDWARE", new string('a', length), ValidDescription);

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(Category.NameMinLength - 1)]
    [InlineData(Category.NameMaxLength + 1)]
    public void Create_NameLengthOutOfRange_ReturnsNameLengthError(int length)
    {
        var result = Category.Create("HARDWARE", new string('a', length), ValidDescription);

        result.Error.ShouldBe(CategoryErrors.NameLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Category.DescriptionMaxLength)]
    public void Create_DescriptionWithinLimit_Succeeds(int length)
    {
        var result = Category.Create("HARDWARE", ValidName, new string('a', length));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_DescriptionTooLong_ReturnsDescriptionLengthError()
    {
        var result = Category.Create("HARDWARE", ValidName, new string('a', Category.DescriptionMaxLength + 1));

        result.Error.ShouldBe(CategoryErrors.DescriptionLength);
    }

    [Fact]
    public void Rename_ValidName_ChangesName()
    {
        var category = TestData.CreateCategory();

        var result = category.Rename("  Eszközök ");

        result.IsSuccess.ShouldBeTrue();
        category.Name.ShouldBe("Eszközök");
    }

    [Fact]
    public void Rename_TooShortName_ReturnsNameLengthErrorAndKeepsName()
    {
        var category = TestData.CreateCategory();

        var result = category.Rename("X");

        result.Error.ShouldBe(CategoryErrors.NameLength);
        category.Name.ShouldBe(ValidName);
    }

    [Fact]
    public void UpdateDescription_ValidDescription_ChangesDescription()
    {
        var category = TestData.CreateCategory();

        var result = category.UpdateDescription("Nyomtatók, monitorok.");

        result.IsSuccess.ShouldBeTrue();
        category.Description.ShouldBe("Nyomtatók, monitorok.");
    }

    [Fact]
    public void UpdateDescription_TooLongDescription_ReturnsDescriptionLengthErrorAndKeepsDescription()
    {
        var category = TestData.CreateCategory();
        var descriptionBefore = category.Description;

        var result = category.UpdateDescription(new string('a', Category.DescriptionMaxLength + 1));

        result.Error.ShouldBe(CategoryErrors.DescriptionLength);
        category.Description.ShouldBe(descriptionBefore);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Deactivate_ActiveCategory_BecomesInactiveAndKeepsData()
    {
        var category = TestData.CreateCategory();

        category.Deactivate();

        category.IsActive.ShouldBeFalse();
        category.Code.ShouldBe("HARDWARE");
        category.Name.ShouldBe(ValidName);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Deactivate_InactiveCategory_StaysInactive()
    {
        var category = TestData.CreateInactiveCategory();

        category.Deactivate();

        category.IsActive.ShouldBeFalse();
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Activate_InactiveCategory_BecomesActive()
    {
        var category = TestData.CreateInactiveCategory();

        category.Activate();

        category.IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Deactivate_CategoryOfExistingTicket_TicketKeepsCategoryAndCanProgress()
    {
        var category = TestData.CreateCategory();
        var ticket = TestData.CreateTicket(category);

        category.Deactivate();
        var result = ticket.AssignTo(TestData.CreateAgent(category), null, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.CategoryId.ShouldBe(category.Id);
        ticket.Status.ShouldBe(TicketStatus.Assigned);
    }
}
