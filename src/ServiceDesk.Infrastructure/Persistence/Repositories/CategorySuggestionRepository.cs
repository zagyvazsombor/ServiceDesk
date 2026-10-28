using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.CategorySuggestions;

namespace ServiceDesk.Infrastructure.Persistence.Repositories;

internal sealed class CategorySuggestionRepository(AppDbContext dbContext) : ICategorySuggestionRepository
{
    public Task<CategorySuggestion?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.CategorySuggestions.SingleOrDefaultAsync(suggestion => suggestion.Id == id, ct);

    public void Add(CategorySuggestion suggestion) => dbContext.CategorySuggestions.Add(suggestion);
}
