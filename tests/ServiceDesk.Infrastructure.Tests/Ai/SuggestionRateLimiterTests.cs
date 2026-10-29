using ServiceDesk.Infrastructure.Ai;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Ai;

// AGENTS.md §8: 5 suggestion requests per minute for each user. The window is real time, so only the limit within
// one window is tested.
public sealed class SuggestionRateLimiterTests
{
    [Fact]
    public void TryAcquire_SixthRequestWithinTheWindow_IsRefused()
    {
        // Arrange
        using var limiter = new SuggestionRateLimiter();

        // Act
        var results = Enumerable.Range(0, SuggestionRateLimiter.PermitLimit + 1)
            .Select(_ => limiter.TryAcquire("user-1"))
            .ToList();

        // Assert
        results.ShouldBe([true, true, true, true, true, false]);
    }

    [Fact]
    public void TryAcquire_OtherUserAfterTheLimitOfTheFirst_IsAllowed()
    {
        // Arrange
        using var limiter = new SuggestionRateLimiter();
        for (var i = 0; i < SuggestionRateLimiter.PermitLimit; i++)
        {
            limiter.TryAcquire("user-1");
        }

        // Act
        var result = limiter.TryAcquire("user-2");

        // Assert
        result.ShouldBeTrue();
        limiter.TryAcquire("user-1").ShouldBeFalse();
    }
}
