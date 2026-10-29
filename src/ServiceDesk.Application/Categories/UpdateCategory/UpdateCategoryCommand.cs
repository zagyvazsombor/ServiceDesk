namespace ServiceDesk.Application.Categories.UpdateCategory;

// AGENTS.md §8: the code cannot be changed, so it is not part of the command. The description is optional: null counts
// as empty.
public sealed record UpdateCategoryCommand(Guid CategoryId, string Name, string? Description);
