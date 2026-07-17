namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Composes the membership, update, and already-covered derivers into the queue
/// output for a single pull request: a <see cref="QueueItem"/> when the pull
/// request is shown, or <see langword="null"/> when it is hidden. Pure; imposes
/// no ordering.
/// </summary>
internal static class QueueItemDeriver
{
    /// <summary>
    /// Derives the queue item for a pull request relative to the user, or
    /// <see langword="null"/> when the pull request is not shown.
    /// </summary>
    /// <param name="facts">The pull request's current facts.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <param name="lastSeen">
    /// The instant the user last looked at the pull request, or
    /// <see langword="null"/> if they never have.
    /// </param>
    /// <returns>
    /// A <see cref="QueueItem"/> when the pull request is shown; otherwise
    /// <see langword="null"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="myLogin"/> is null, empty, or whitespace.
    /// </exception>
    public static QueueItem? Derive(
        PullRequestFacts facts,
        string myLogin,
        DateTimeOffset? lastSeen
    )
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        if (MembershipDeriver.Derive(facts, myLogin).State is not { } state)
        {
            return null;
        }

        return new QueueItem(
            facts.Identity,
            new LastUpdate(facts.Status.LastUpdatedBy, facts.Status.LastUpdatedAt),
            state,
            UpdateDetector.HasUpdate(facts, myLogin, lastSeen),
            ReviewerRosterDeriver.Derive(facts, myLogin),
            new MyEngagement(lastSeen, LastReviewedByMe(facts, myLogin)),
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
