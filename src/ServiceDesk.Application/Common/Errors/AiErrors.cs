using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Errors;

public static class AiErrors
{
    // BR-08: invalid, missing or late model answer; the ticket can still be created.
    public static readonly Error NoSuggestion = new(
        "Ai.NoSuggestion",
        "Most nem sikerült kategóriát javasolni, válassz kézzel.",
        ErrorType.Unavailable);

    // AGENTS.md §8: more than 5 suggestion requests of the user within a minute.
    public static readonly Error RateLimited = new(
        "Ai.RateLimited",
        "Túl sok javaslatkérés, próbáld újra egy perc múlva, vagy válassz kézzel.",
        ErrorType.Unavailable);
}
