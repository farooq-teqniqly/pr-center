namespace PrCenter.Core.Facts;

/// <summary>
/// The reviewable activity on a pull request that the queue derivers read: the
/// directly requested reviewers, the submitted reviews, and the update-worthy
/// event timelines (commits and comments). Immutable data carrier with no
/// derivation behavior.
/// </summary>
public sealed record PullRequestActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestActivity"/> class.
    /// </summary>
    /// <param name="requestedReviewerLogins">The logins of directly requested reviewers (team-routed requests excluded).</param>
    /// <param name="reviews">The submitted reviews.</param>
    /// <param name="commits">The commits that landed on the branch.</param>
    /// <param name="comments">The comments and replies.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="requestedReviewerLogins"/>,
    /// <paramref name="reviews"/>, <paramref name="commits"/>, or
    /// <paramref name="comments"/> is null.
    /// </exception>
    public PullRequestActivity(
        IReadOnlyList<string> requestedReviewerLogins,
        IReadOnlyList<ReviewFact> reviews,
        IReadOnlyList<CommitFact> commits,
        IReadOnlyList<CommentFact> comments
    )
    {
        ArgumentNullException.ThrowIfNull(requestedReviewerLogins);
        ArgumentNullException.ThrowIfNull(reviews);
        ArgumentNullException.ThrowIfNull(commits);
        ArgumentNullException.ThrowIfNull(comments);

        RequestedReviewerLogins = requestedReviewerLogins;
        Reviews = reviews;
        Commits = commits;
        Comments = comments;
    }

    /// <summary>Gets the logins of directly requested reviewers (team-routed requests excluded).</summary>
    public IReadOnlyList<string> RequestedReviewerLogins { get; }

    /// <summary>Gets the submitted reviews.</summary>
    public IReadOnlyList<ReviewFact> Reviews { get; }

    /// <summary>Gets the commits that landed on the branch.</summary>
    public IReadOnlyList<CommitFact> Commits { get; }

    /// <summary>Gets the comments and replies.</summary>
    public IReadOnlyList<CommentFact> Comments { get; }
}
