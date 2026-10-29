namespace ServiceDesk.Application.Tickets.GetActiveCategories;

// Id: the value of CreateTicketCommand.CategoryId.
public sealed record ActiveCategoryDto(Guid Id, string Code, string Name, string Description);
