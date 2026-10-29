using System.Threading.RateLimiting;
using ServiceDesk.Application.Common.Ai;

namespace ServiceDesk.Infrastructure.Ai;

// AGENTS.md §8: a fixed window of 5 requests per minute for each user. A singleton, so the window of a user is shared
// by all of their circuits; the DI container disposes it with the host.
internal sealed class SuggestionRateLimiter : ISuggestionRateLimiter, IDisposable
{
    public const int PermitLimit = 5;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // QueueLimit 0: a request over the limit is refused at once instead of waiting for the next window.
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<string, string>(
        userId => RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = PermitLimit,
            Window = Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    public bool TryAcquire(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        // A fixed window lease does not give its permit back when disposed.
        using var lease = _limiter.AttemptAcquire(userId);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
