using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Application.Common.Persistence;

public interface ICategoryRepository
{
    // null if the category does not exist.
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct);

    void Add(Category category);
}
