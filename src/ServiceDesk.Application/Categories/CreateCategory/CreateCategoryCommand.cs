namespace ServiceDesk.Application.Categories.CreateCategory;

// A new category is always active. The description is optional: null counts as empty.
public sealed record CreateCategoryCommand(string Code, string Name, string? Description);
