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
    /// Thrown when any collection contains a null element, or when
    /// <paramref name="requestedReviewerLogins"/> contains a null or blank login.
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

        // Copy each collection into a read-only wrapper so the snapshot cannot be
        // mutated -- neither through the caller's original reference nor by casting
        // the property back to an array. Reject null elements (and blank logins) up
        // front so a deriver never sees garbage.
        RequestedReviewerLogins = SealLogins(
            requestedReviewerLogins,
            nameof(requestedReviewerLogins)
        );
        Reviews = SealWithoutNulls(reviews, nameof(reviews));
        Commits = SealWithoutNulls(commits, nameof(commits));
        Comments = SealWithoutNulls(comments, nameof(comments));
    }

    private static IReadOnlyList<string> SealLogins(IReadOnlyList<string> logins, string paramName)
    {
        var copy = logins.ToArray();

        if (Array.Exists(copy, static login => string.IsNullOrWhiteSpace(login)))
        {
            throw new ArgumentException("Collection contains a null or blank login.", paramName);
        }

        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<T> SealWithoutNulls<T>(IReadOnlyList<T> items, string paramName)
        where T : class
    {
        var copy = items.ToArray();

        if (Array.Exists(copy, static item => item is null))
        {
            throw new ArgumentException("Collection contains a null element.", paramName);
        }

        return Array.AsReadOnly(copy);
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
