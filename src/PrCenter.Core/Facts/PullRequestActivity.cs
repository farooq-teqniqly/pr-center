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
    /// <exception cref="ArgumentException">
    /// Thrown when any of the collections contains a null element.
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

        // Copy each collection so the snapshot cannot be mutated through the
        // caller's reference, and reject null elements up front so a deriver
        // never dereferences one.
        RequestedReviewerLogins = CopyWithoutNulls(
            requestedReviewerLogins,
            nameof(requestedReviewerLogins)
        );
        Reviews = CopyWithoutNulls(reviews, nameof(reviews));
        Commits = CopyWithoutNulls(commits, nameof(commits));
        Comments = CopyWithoutNulls(comments, nameof(comments));
    }

    private static IReadOnlyList<T> CopyWithoutNulls<T>(IReadOnlyList<T> items, string paramName)
        where T : class
    {
        var copy = items.ToArray();

        if (Array.Exists(copy, item => item is null))
        {
            throw new ArgumentException("Collection contains a null element.", paramName);
        }

        return copy;
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
