using ServiceDesk.Domain.CategorySuggestions;

namespace ServiceDesk.Application.Tickets.GetTicketDetails;

// BR-09: the AI suggestion decided on the create form of the ticket, for the Admin. Confidence is between 0 and 1;
// Reasoning is the plain text of the model. Decision is Accepted or Overridden.
public sealed record TicketCategorySuggestionDto(
    string SuggestedCategoryName,
    double Confidence,
    string Reasoning,
    CategorySuggestionDecision Decision,
    string Model,
    string PromptVersion,
    DateTimeOffset? DecidedAt);
