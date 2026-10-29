using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Ai;

// AGENTS.md §9: the LLM category suggestion. The adapter validates the model answer (BR-08): the code is one of the
// input categories, the confidence is between 0 and 1, the reasoning is plain text of at most 500 characters.
// Every model failure (invalid answer, exception, timeout) is AiErrors.NoSuggestion, never an exception; only a
// cancellation requested through ct is thrown.
public interface ICategorySuggester
{
    Task<Result<CategorySuggestionOutput>> SuggestAsync(CategorySuggestionInput input, CancellationToken ct);
}
