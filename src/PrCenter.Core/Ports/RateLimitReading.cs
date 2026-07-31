namespace PrCenter.Core.Ports;

/// <summary>
/// GitHub's rate-limit state as of one review-queue request, read from the
/// response body's `rateLimit` field rather than the `x-ratelimit-*` headers:
/// the GraphQL API bills in points rather than requests, so a header count does
/// not describe what the query consumed. Diagnostic only -- nothing in the
/// derivation path reads it.
/// </summary>
/// <param name="Remaining">The point allowance left in the current window.</param>
/// <param name="ResetAt">The instant the current window resets and the allowance is restored.</param>
/// <param name="Cost">The points this query consumed; the number that predicts when the limit will bite.</param>
public sealed record RateLimitReading(int Remaining, DateTimeOffset ResetAt, int Cost);
