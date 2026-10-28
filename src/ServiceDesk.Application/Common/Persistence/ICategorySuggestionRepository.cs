using ServiceDesk.Domain.CategorySuggestions;

namespace ServiceDesk.Application.Common.Persistence;

public interface ICategorySuggestionRepository
{
    // null if the suggestion does not exist.
    Task<CategorySuggestion?> GetByIdAsync(Guid id, CancellationToken ct);

    void Add(CategorySuggestion suggestion);
}
