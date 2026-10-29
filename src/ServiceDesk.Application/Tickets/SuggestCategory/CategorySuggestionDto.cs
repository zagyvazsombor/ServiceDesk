namespace ServiceDesk.Application.Tickets.SuggestCategory;

// SuggestionId: the saved, still Pending suggestion the create form decides about (BR-09).
// CategoryId: an active category, a value of CreateTicketCommand.CategoryId. Confidence is between 0 and 1.
public sealed record CategorySuggestionDto(
    Guid SuggestionId,
    Guid CategoryId,
    string CategoryName,
    double Confidence,
    string Reasoning);
