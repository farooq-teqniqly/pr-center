namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Derives a pull request's queue membership relative to the user, as a pure
/// function of current facts with no stored transition history. Encodes the
/// draft/closed early-outs and the latest-review-verdict rule from the state
/// model.
/// </summary>
internal static class MembershipDeriver
{
    /// <summary>
    /// Derives the membership of a pull request relative to the given user.
    /// </summary>
    /// <param name="facts">The pull request's current facts.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <returns>
    /// A shown result with a <see cref="MembershipState"/>, or a hidden result
    /// with a <see cref="MembershipExclusion"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="myLogin"/> is null, empty, or whitespace.
    /// </exception>
    public static MembershipResult Derive(PullRequestFacts facts, string myLogin)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        if (facts.Status.IsDraft)
        {
            return MembershipResult.Hidden(MembershipExclusion.Draft);
        }

        if (facts.Status.IsClosedOrMerged)
        {
            return MembershipResult.Hidden(MembershipExclusion.ClosedOrMerged);
        }

        var myLatest = LatestReviewBy(facts, myLogin);

        if (myLatest is { State: ReviewState.Commented or ReviewState.ChangesRequested })
        {
            return MembershipResult.Shown(MembershipState.AwaitingReReview);
        }

        var amRequested = facts.Activity.RequestedReviewerLogins.Any(login =>
            GitHubLogin.IsMe(login, myLogin)
        );

        if (amRequested)
        {
            return MembershipResult.Shown(MembershipState.AwaitingFirstReview);
        }

        return myLatest is null
            ? MembershipResult.Hidden(MembershipExclusion.Untracked)
            : MembershipResult.Hidden(MembershipExclusion.Approved);
    }

    private static ReviewFact? LatestReviewBy(PullRequestFacts facts, string myLogin) =>
        facts
            .Activity.Reviews.Where(review => GitHubLogin.IsMe(review.ReviewerLogin, myLogin))
            .OrderByDescending(review => review.SubmittedAt)
            .ThenBy(review => TieBreakRank(review.State))
            .FirstOrDefault();

    // Tie-break only, applied when two of the user's reviews share the same
    // SubmittedAt (GitHub timestamps are second-granularity). The most
    // actionable verdict wins so a tie keeps the pull request in the queue
    // rather than dropping it: non-approved (owes a re-review) outranks
    // Approved. It never overrides a strictly-later review. Commented -- and any
    // unknown/future state -- falls to the lowest rank (most actionable, keep
    // shown), so there is no unreachable throwing arm.
    private static int TieBreakRank(ReviewState state) =>
        state switch
        {
            ReviewState.Approved => 2,
            ReviewState.ChangesRequested => 1,
            _ => 0,
        };
}
