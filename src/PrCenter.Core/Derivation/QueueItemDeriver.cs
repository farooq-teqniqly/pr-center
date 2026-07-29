namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Composes the membership, update, and already-covered derivers into the queue
/// output for a single pull request: a <see cref="QueueItem"/> when the pull
/// request is shown, or the <see cref="MembershipExclusion"/> that hid it when it
/// is not. Pure; imposes no ordering.
/// </summary>
internal static class QueueItemDeriver
{
    /// <summary>
    /// Derives the queue outcome for a pull request relative to the user.
    /// </summary>
    /// <param name="facts">The pull request's current facts.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <returns>
    /// A shown <see cref="QueueItemResult"/> carrying the <see cref="QueueItem"/>
    /// when the pull request is shown; otherwise a hidden result carrying the
    /// exclusion reason.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="myLogin"/> is null, empty, or whitespace.
    /// </exception>
    public static QueueItemResult Derive(PullRequestFacts facts, string myLogin)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        var membership = MembershipDeriver.Derive(facts, myLogin);

        // A result with no state is hidden, and MembershipResult's factories make
        // a hidden result without an exclusion unrepresentable.
        return membership.State is { } state
            ? QueueItemResult.Shown(ShownItem(facts, myLogin, state))
            : QueueItemResult.Hidden(
                membership.Exclusion
                    ?? throw new InvalidOperationException(
                        "A hidden membership result carried no exclusion."
                    )
            );
    }

    private static QueueItem ShownItem(
        PullRequestFacts facts,
        string myLogin,
        MembershipState state
    )
    {
        // The user's latest review is both the displayed engagement instant and
        // the update baseline, so has-update and "when I last reviewed" are
        // provably the same instant.
        var myLastReviewedAt = LastReviewedByMe(facts, myLogin);

        return new QueueItem(
            facts.Identity,
            new LastUpdate(facts.Status.LastUpdatedBy, facts.Status.LastUpdatedAt),
            state,
            UpdateDetector.HasUpdate(facts, myLogin, myLastReviewedAt),
            ReviewerRosterDeriver.Derive(facts, myLogin),
            new MyEngagement(myLastReviewedAt),
            CoveredFlag.CoveringLogins(facts, myLogin)
        );
    }

    // The instant of the user's latest review regardless of its state, or null
    // when they have never reviewed -- "when I last reviewed" is a fact about my
    // activity, not about whether that review still stands.
    private static DateTimeOffset? LastReviewedByMe(PullRequestFacts facts, string myLogin) =>
        facts
            .Activity.Reviews.Where(review => GitHubLogin.IsMe(review.ReviewerLogin, myLogin))
            .Select(review => (DateTimeOffset?)review.SubmittedAt)
            .Max();
}
