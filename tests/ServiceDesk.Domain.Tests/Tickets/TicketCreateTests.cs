using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Tickets;

public sealed class TicketCreateTests
{
    private readonly Category _category = TestData.CreateCategory();

    [Fact]
    [Trait("BR", "BR-01")]
    public void Create_ValidInput_ReturnsNewUnassignedTicket()
    {
        var result = Ticket.Create(
            TestData.ReporterId,
            TestData.ValidTitle,
            TestData.ValidDescription,
            Impact.High,
            Urgency.Low,
            _category,
            TestData.Now);

        result.IsSuccess.ShouldBeTrue();
        var ticket = result.Value;
        ticket.Id.ShouldNotBe(Guid.Empty);
        ticket.ReporterId.ShouldBe(TestData.ReporterId);
        ticket.Title.ShouldBe(TestData.ValidTitle);
        ticket.Description.ShouldBe(TestData.ValidDescription);
        ticket.Impact.ShouldBe(Impact.High);
        ticket.Urgency.ShouldBe(Urgency.Low);
        ticket.CategoryId.ShouldBe(_category.Id);
        ticket.Status.ShouldBe(TicketStatus.New);
        ticket.AssigneeAgentId.ShouldBeNull();
        ticket.CreatedAt.ShouldBe(TestData.Now);
    }

    [Theory]
    [Trait("BR", "BR-01")]
    [InlineData(Ticket.TitleMinLength)]
    [InlineData(Ticket.TitleMaxLength)]
    public void Create_TitleLengthAtLimit_Succeeds(int length)
    {
        var result = CreateWith(title: new string('a', length));

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [Trait("BR", "BR-01")]
    [InlineData(0)]
    [InlineData(Ticket.TitleMinLength - 1)]
    [InlineData(Ticket.TitleMaxLength + 1)]
    public void Create_TitleLengthOutOfRange_ReturnsTitleLengthError(int length)
    {
        var result = CreateWith(title: new string('a', length));

        result.Error.ShouldBe(TicketErrors.TitleLength);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Create_TitleWithSurroundingWhitespace_StoresTrimmedTitle()
    {
        var result = CreateWith(title: "   Nem indul a laptop   ");

        result.Value.Title.ShouldBe("Nem indul a laptop");
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Create_TitleTooShortAfterTrim_ReturnsTitleLengthError()
    {
        var result = CreateWith(title: "   abcd   ");

        result.Error.ShouldBe(TicketErrors.TitleLength);
    }

    [Theory]
    [Trait("BR", "BR-01")]
    [InlineData(Ticket.DescriptionMinLength)]
    [InlineData(Ticket.DescriptionMaxLength)]
    public void Create_DescriptionLengthAtLimit_Succeeds(int length)
    {
        var result = CreateWith(description: new string('a', length));

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [Trait("BR", "BR-01")]
    [InlineData(Ticket.DescriptionMinLength - 1)]
    [InlineData(Ticket.DescriptionMaxLength + 1)]
    public void Create_DescriptionLengthOutOfRange_ReturnsDescriptionLengthError(int length)
    {
        var result = CreateWith(description: new string('a', length));

        result.Error.ShouldBe(TicketErrors.DescriptionLength);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Create_WhitespaceOnlyDescription_ReturnsDescriptionLengthError()
    {
        var result = CreateWith(description: new string(' ', Ticket.DescriptionMinLength));

        result.Error.ShouldBe(TicketErrors.DescriptionLength);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Create_UndefinedImpact_ReturnsImpactInvalidError()
    {
        var result = CreateWith(impact: (Impact)99);

        result.Error.ShouldBe(TicketErrors.ImpactInvalid);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Create_UndefinedUrgency_ReturnsUrgencyInvalidError()
    {
        var result = CreateWith(urgency: (Urgency)99);

        result.Error.ShouldBe(TicketErrors.UrgencyInvalid);
    }

    [Fact]
    [Trait("BR", "BR-01")]
    public void Create_NullTitle_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => CreateWith(title: null!));
    }

    [Theory]
    [Trait("BR", "BR-01")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingReporterId_ThrowsArgumentException(string reporterId)
    {
        Should.Throw<ArgumentException>(() => Ticket.Create(
            reporterId,
            TestData.ValidTitle,
            TestData.ValidDescription,
            Impact.Medium,
            Urgency.Medium,
            _category,
            TestData.Now));
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Create_ActiveCategory_StoresCategoryId()
    {
        var category = TestData.CreateCategory("NETWORK");

        var result = CreateWith(category: category);

        result.Value.CategoryId.ShouldBe(category.Id);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Create_InactiveCategory_ReturnsCategoryInactiveError()
    {
        var result = CreateWith(category: TestData.CreateInactiveCategory());

        result.Error.ShouldBe(CategoryErrors.Inactive);
    }

    [Theory]
    [Trait("BR", "BR-11")]
    [InlineData(Impact.High, Urgency.High, Priority.Critical)]
    [InlineData(Impact.Medium, Urgency.High, Priority.High)]
    [InlineData(Impact.Low, Urgency.Low, Priority.Planning)]
    public void Create_ImpactAndUrgency_SetsPriorityFromMatrix(Impact impact, Urgency urgency, Priority expected)
    {
        var result = CreateWith(impact: impact, urgency: urgency);

        result.Value.Priority.ShouldBe(expected);
    }

    private Result<Ticket> CreateWith(
        string title = TestData.ValidTitle,
        string description = TestData.ValidDescription,
        Impact impact = Impact.Medium,
        Urgency urgency = Urgency.Medium,
        Category? category = null) =>
        Ticket.Create(TestData.ReporterId, title, description, impact, urgency, category ?? _category, TestData.Now);
}
