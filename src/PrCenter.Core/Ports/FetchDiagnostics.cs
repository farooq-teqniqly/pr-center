namespace PrCenter.Core.Ports;

/// <summary>
/// The facts about one owner-queue fetch that exist only inside the GitHub
/// adapter and are otherwise unobservable to the caller that records them: how
/// many nodes each discovery search returned before the union, and the
/// rate-limit reading for the request.
/// </summary>
/// <remarks>
/// The post-union count is deliberately absent. It is already
/// <see cref="OwnerFactsResult.Facts"/>.Count, so carrying it here would make
/// two sources of truth for one number.
/// </remarks>
/// <param name="RequestedCount">
/// The node count of the review-requested search, counted before deduplication.
/// A pull request matching both searches is counted in both.
/// </param>
/// <param name="ReviewedCount">
/// The node count of the reviewed-by search, counted before deduplication.
/// A pull request matching both searches is counted in both.
/// </param>
/// <param name="RateLimit">
/// The rate-limit reading for the request, or <see langword="null"/> when the
/// response omitted or malformed the field. A missing reading never fails the
/// fetch: the rate limit is diagnostic, and losing it must not cost the user
/// their queue.
/// </param>
public sealed record FetchDiagnostics(
    int RequestedCount,
    int ReviewedCount,
    RateLimitReading? RateLimit
);
