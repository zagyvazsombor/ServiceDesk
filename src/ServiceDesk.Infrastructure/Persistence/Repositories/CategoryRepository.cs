using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(AppDbContext dbContext) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Categories.SingleOrDefaultAsync(category => category.Id == id, ct);

    public async Task<IReadOnlyList<Category>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await dbContext.Categories
            .Where(category => ids.Contains(category.Id))
            .ToListAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        dbContext.Categories.AnyAsync(category => category.Code == code, ct);

    public void Add(Category category) => dbContext.Categories.Add(category);
}
