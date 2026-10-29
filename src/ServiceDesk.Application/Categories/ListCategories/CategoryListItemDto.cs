namespace ServiceDesk.Application.Categories.ListCategories;

// Id: the value of the category commands. IsActive is false for a deactivated category (BR-13).
public sealed record CategoryListItemDto(Guid Id, string Code, string Name, string Description, bool IsActive);
