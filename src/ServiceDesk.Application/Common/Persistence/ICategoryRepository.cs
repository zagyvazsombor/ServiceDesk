using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Application.Common.Persistence;

public interface ICategoryRepository
{
    // null if the category does not exist.
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct);

    // The existing categories among the Ids; an unknown Id is simply missing from the list.
    Task<IReadOnlyList<Category>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    // BR-13: the active categories, the only ones a new ticket or an AI suggestion can get.
    Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct);

    // Whether a category, active or inactive, already has the code (Category.Code is unique).
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);

    void Add(Category category);
}
