using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Domain.Tests.CategorySuggestions;

public sealed class CategorySuggestionTests
{
    private const string ValidReasoning = "A leírás szerint a laptop nem kapcsol be.";
    private const string ValidModel = "gemma3:4b";
    private const string ValidPromptVersion = "v1";

    private readonly Category _category = TestData.CreateCategory();

    [Fact]
    [Trait("BR", "BR-09")]
    public void Create_ValidInput_ReturnsPendingSuggestion()
    {
        var result = CategorySuggestion.Create(
            TestData.ReporterId,
            _category,
            0.82,
            ValidReasoning,
            ValidModel,
            ValidPromptVersion,
            TestData.Now);

        result.IsSuccess.ShouldBeTrue();
        var suggestion = result.Value;
        suggestion.Id.ShouldNotBe(Guid.Empty);
        suggestion.RequestedByUserId.ShouldBe(TestData.ReporterId);
        suggestion.SuggestedCategoryId.ShouldBe(_category.Id);
        suggestion.Confidence.ShouldBe(0.82);
        suggestion.Reasoning.ShouldBe(ValidReasoning);
        suggestion.Model.ShouldBe(ValidModel);
        suggestion.PromptVersion.ShouldBe(ValidPromptVersion);
        suggestion.CreatedAt.ShouldBe(TestData.Now);
        suggestion.Decision.ShouldBe(CategorySuggestionDecision.Pending);
        suggestion.TicketId.ShouldBeNull();
        suggestion.DecidedAt.ShouldBeNull();
    }

    [Fact]
    [Trait("BR", "BR-08")]
    [Trait("BR", "BR-13")]
    public void Create_InactiveCategory_ReturnsCategoryInactiveError()
    {
        var result = CreateWith(category: TestData.CreateInactiveCategory());

        result.Error.ShouldBe(CategoryErrors.Inactive);
    }

    [Theory]
    [Trait("BR", "BR-08")]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Create_ConfidenceAtLimit_Succeeds(double confidence)
    {
        var result = CreateWith(confidence: confidence);

        result.Value.Confidence.ShouldBe(confidence);
    }

    [Theory]
    [Trait("BR", "BR-08")]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    public void Create_ConfidenceOutsideRange_ThrowsArgumentOutOfRangeException(double confidence)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateWith(confidence: confidence));
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public void Create_ReasoningAtMaxLength_Succeeds()
    {
        var reasoning = new string('a', CategorySuggestion.ReasoningMaxLength);

        var result = CreateWith(reasoning: reasoning);

        result.Value.Reasoning.ShouldBe(reasoning);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public void Create_ReasoningTooLong_ThrowsArgumentOutOfRangeException()
    {
        var reasoning = new string('a', CategorySuggestion.ReasoningMaxLength + 1);

        Should.Throw<ArgumentOutOfRangeException>(() => CreateWith(reasoning: reasoning));
    }

    [Fact]
    public void Create_ModelTooLong_ThrowsArgumentOutOfRangeException()
    {
        var model = new string('a', CategorySuggestion.ModelMaxLength + 1);

        Should.Throw<ArgumentOutOfRangeException>(() => CreateWith(model: model));
    }

    [Fact]
    public void Create_PromptVersionTooLong_ThrowsArgumentOutOfRangeException()
    {
        var promptVersion = new string('a', CategorySuggestion.PromptVersionMaxLength + 1);

        Should.Throw<ArgumentOutOfRangeException>(() => CreateWith(promptVersion: promptVersion));
    }

    [Fact]
    public void Create_NullReasoning_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => CreateWith(reasoning: null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingRequestedByUserId_ThrowsArgumentException(string requestedByUserId)
    {
        Should.Throw<ArgumentException>(() => CategorySuggestion.Create(
            requestedByUserId,
            _category,
            0.5,
            ValidReasoning,
            ValidModel,
            ValidPromptVersion,
            TestData.Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingModel_ThrowsArgumentException(string model)
    {
        Should.Throw<ArgumentException>(() => CreateWith(model: model));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingPromptVersion_ThrowsArgumentException(string promptVersion)
    {
        Should.Throw<ArgumentException>(() => CreateWith(promptVersion: promptVersion));
    }

    [Theory]
    [Trait("BR", "BR-09")]
    [InlineData(CategorySuggestionDecision.Accepted)]
    [InlineData(CategorySuggestionDecision.Overridden)]
    public void Decide_PendingSuggestion_RecordsTicketDecisionAndTime(CategorySuggestionDecision decision)
    {
        // Arrange
        var suggestion = CreateWith().Value;
        var ticketId = Guid.CreateVersion7();
        var decidedAt = TestData.Now.AddMinutes(3);

        // Act
        var result = suggestion.Decide(ticketId, decision, decidedAt);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        suggestion.TicketId.ShouldBe(ticketId);
        suggestion.Decision.ShouldBe(decision);
        suggestion.DecidedAt.ShouldBe(decidedAt);
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public void Decide_AlreadyDecided_ReturnsAlreadyDecidedAndKeepsFirstDecision()
    {
        // Arrange
        var suggestion = CreateWith().Value;
        var firstTicketId = Guid.CreateVersion7();
        suggestion.Decide(firstTicketId, CategorySuggestionDecision.Accepted, TestData.Now).IsSuccess.ShouldBeTrue();

        // Act
        var result = suggestion.Decide(Guid.CreateVersion7(), CategorySuggestionDecision.Overridden, TestData.Now.AddMinutes(1));

        // Assert
        result.Error.ShouldBe(CategorySuggestionErrors.AlreadyDecided);
        suggestion.TicketId.ShouldBe(firstTicketId);
        suggestion.Decision.ShouldBe(CategorySuggestionDecision.Accepted);
        suggestion.DecidedAt.ShouldBe(TestData.Now);
    }

    [Theory]
    [InlineData(CategorySuggestionDecision.Pending)]
    [InlineData((CategorySuggestionDecision)99)]
    public void Decide_NotAHumanDecision_ThrowsArgumentOutOfRangeException(CategorySuggestionDecision decision)
    {
        var suggestion = CreateWith().Value;

        Should.Throw<ArgumentOutOfRangeException>(() => suggestion.Decide(Guid.CreateVersion7(), decision, TestData.Now));
    }

    [Fact]
    public void Decide_EmptyTicketId_ThrowsArgumentException()
    {
        var suggestion = CreateWith().Value;

        Should.Throw<ArgumentException>(() => suggestion.Decide(Guid.Empty, CategorySuggestionDecision.Accepted, TestData.Now));
    }

    private Result<CategorySuggestion> CreateWith(
        Category? category = null,
        double confidence = 0.5,
        string reasoning = ValidReasoning,
        string model = ValidModel,
        string promptVersion = ValidPromptVersion) =>
        CategorySuggestion.Create(
            TestData.ReporterId,
            category ?? _category,
            confidence,
            reasoning,
            model,
            promptVersion,
            TestData.Now);
}
