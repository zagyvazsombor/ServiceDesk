using ServiceDesk.Domain.Common;

namespace ServiceDesk.Domain.CategorySuggestions;

public static class CategorySuggestionErrors
{
    // BR-09: also the answer for a suggestion of another user, so its existence is not revealed.
    public static readonly Error NotFound = new(
        "CategorySuggestion.NotFound",
        "A kategóriajavaslat nem található, kérj újat, vagy válassz kézzel.",
        ErrorType.NotFound);

    // BR-09: the human decision is recorded once, together with the ticket it led to.
    public static readonly Error AlreadyDecided = new(
        "CategorySuggestion.AlreadyDecided",
        "Erről a kategóriajavaslatról már döntöttek, kérj újat, vagy válassz kézzel.",
        ErrorType.Conflict);
}
