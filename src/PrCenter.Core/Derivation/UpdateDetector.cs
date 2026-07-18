namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Detects whether a pull request has an update relative to the user's latest
/// review: another person's commit, or a human comment or review, strictly after
/// that review. The user's own activity, bare metadata bumps, and bot/CI comments
/// and reviews never count; bot commits do (a new commit is a real diff). With no
/// review baseline the pull request is new, not updated. Pure function of current
/// facts and the review instant.
/// </summary>
internal static class UpdateDetector
{
    /// <summary>
    /// Determines whether a pull request has an update relative to the user since
    /// their latest review.
    /// </summary>
    /// <param name="facts">The pull request's current facts.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <param name="myLastReviewedAt">
    /// The instant the user last submitted a review on the pull request, or
    /// <see langword="null"/> if they never have.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when another person's activity landed strictly after
    /// the user's latest review; <see langword="false"/> otherwise, including when
    /// the user has never reviewed the pull request.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="myLogin"/> is null, empty, or whitespace.
    /// </exception>
    public static bool HasUpdate(
        PullRequestFacts facts,
        string myLogin,
        DateTimeOffset? myLastReviewedAt
    )
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        // No review baseline means the pull request is new, not updated: the
        // update indicator is meaningful only relative to a review of my own.
        if (myLastReviewedAt is not { } baseline)
        {
            return false;
        }

        return UpdateEvents(facts.Activity)
            .Any(activity => activity.When > baseline && GitHubLogin.NotMe(activity.By, myLogin));
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
