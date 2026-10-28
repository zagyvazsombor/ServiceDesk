using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Categories;

namespace ServiceDesk.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(AppDbContext dbContext) : ICategoryRepository
{
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Categories.SingleOrDefaultAsync(category => category.Id == id, ct);

    public void Add(Category category) => dbContext.Categories.Add(category);
}
