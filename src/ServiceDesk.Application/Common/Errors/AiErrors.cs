using ServiceDesk.Domain.Common;

namespace ServiceDesk.Application.Common.Errors;

public static class AiErrors
{
    // BR-08: invalid, missing or late model answer; the ticket can still be created.
    public static readonly Error NoSuggestion = new(
        "Ai.NoSuggestion",
        "Most nem sikerült kategóriát javasolni, válassz kézzel.",
        ErrorType.Unavailable);
}
