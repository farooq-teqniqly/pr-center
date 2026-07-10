namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Detects whether a pull request has an update the user has not seen: any
/// other person's commit, comment, or review since the last-seen marker. The
/// user's own activity and bare metadata bumps never count. Pure function of
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

    private static IEnumerable<(string By, DateTimeOffset When)> UpdateEvents(
        PullRequestActivity activity
    ) =>
        activity
            .Commits.Select(commit => (By: commit.AuthorLogin, When: commit.LandedAt))
            .Concat(activity.Comments.Select(comment => (comment.AuthorLogin, comment.CreatedAt)))
            .Concat(activity.Reviews.Select(review => (review.ReviewerLogin, review.SubmittedAt)));
}
