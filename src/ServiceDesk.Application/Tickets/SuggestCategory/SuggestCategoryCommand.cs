namespace ServiceDesk.Application.Tickets.SuggestCategory;

// The text of the ticket being filled in on the create form (BR-09); the requesting user comes from ICurrentUser.
public sealed record SuggestCategoryCommand(string Title, string Description);
