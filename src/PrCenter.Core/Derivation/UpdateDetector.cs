namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Detects whether a pull request has an update the user has not seen: another
/// person's commit, or a human comment or review, since the last-seen marker.
/// The user's own activity, bare metadata bumps, and bot/CI comments and reviews
/// never count; bot commits do (a new commit is a real diff). Pure function of
/// current facts and the marker.
/// </summary>
internal static class UpdateDetector
{
    /// <summary>
    /// Determines whether a pull request has an unseen update relative to the user.
    /// </summary>
    /// <param name="facts">The pull request's current facts.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <param name="lastSeen">
    /// The instant the user last looked at the pull request, or
    /// <see langword="null"/> if they never have.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the pull request has an update the user has
    /// not seen; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="myLogin"/> is null, empty, or whitespace.
    /// </exception>
    public static bool HasUpdate(PullRequestFacts facts, string myLogin, DateTimeOffset? lastSeen)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        if (lastSeen is not { } marker)
        {
            return true;
        }

        return UpdateEvents(facts.Activity)
            .Any(activity => activity.When > marker && GitHubLogin.NotMe(activity.By, myLogin));
    }

    // Bot comments and reviews are noise and are dropped here; commits are never
    // filtered because a bot commit is still a real diff to review.
    private static IEnumerable<(string By, DateTimeOffset When)> UpdateEvents(
        PullRequestActivity activity
    ) =>
        activity
            .Commits.Select(commit => (By: commit.AuthorLogin, When: commit.LandedAt))
            .Concat(
                activity
                    .Comments.Where(comment => !comment.IsBot)
                    .Select(comment => (comment.AuthorLogin, comment.CreatedAt))
            )
            .Concat(
                activity
                    .Reviews.Where(review => !review.IsBot)
                    .Select(review => (review.ReviewerLogin, review.SubmittedAt))
            );
}
