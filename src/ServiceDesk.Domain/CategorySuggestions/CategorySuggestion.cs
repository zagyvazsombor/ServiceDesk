using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Domain.CategorySuggestions;

// BR-09: durable trace of an LLM category suggestion and of the human decision about it.
public sealed class CategorySuggestion
{
    public const int ReasoningMaxLength = 500;
    public const int ModelMaxLength = 100;
    public const int PromptVersionMaxLength = 20;

    // Used by EF Core and the factory method.
    private CategorySuggestion()
    {
    }

    public Guid Id { get; private set; }

    // The Reporter who asked for the suggestion on the create form; the same user decides about it (BR-09).
    public string RequestedByUserId { get; private set; } = string.Empty;

    public Guid SuggestedCategoryId { get; private set; }

    // Between 0 and 1.
    public double Confidence { get; private set; }

    public string Reasoning { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public string PromptVersion { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    // Set together with the decision, when the ticket is created (BR-09).
    public Guid? TicketId { get; private set; }

    public CategorySuggestionDecision Decision { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    // The AI adapter validates the model output before this call (BR-08, AGENTS.md §9), so a value out of range is a
    // programming error.
    public static Result<CategorySuggestion> Create(
        string requestedByUserId,
        Category suggestedCategory,
        double confidence,
        string reasoning,
        string model,
        string promptVersion,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedByUserId);
        ArgumentNullException.ThrowIfNull(suggestedCategory);
        ArgumentNullException.ThrowIfNull(reasoning);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptVersion);

        if (double.IsNaN(confidence) || confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0 and 1.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(reasoning.Length, ReasoningMaxLength, nameof(reasoning));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(model.Length, ModelMaxLength, nameof(model));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(promptVersion.Length, PromptVersionMaxLength, nameof(promptVersion));

        if (!suggestedCategory.IsActive)
        {
            return CategoryErrors.Inactive; // BR-08, BR-13
        }

        return new CategorySuggestion
        {
            Id = Guid.CreateVersion7(),
            RequestedByUserId = requestedByUserId,
            SuggestedCategoryId = suggestedCategory.Id,
            Confidence = confidence,
            Reasoning = reasoning,
            Model = model,
            PromptVersion = promptVersion,
            CreatedAt = now,
            Decision = CategorySuggestionDecision.Pending,
        };
    }
}
