namespace ServiceDesk.Application.Common.Ai;

// AGENTS.md §8: the AI suggestion is limited per user (5 requests per minute).
public interface ISuggestionRateLimiter
{
    // true if the user may ask for one more suggestion now; a successful call uses up one request of the window.
    bool TryAcquire(string userId);
}
