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
    /// Thrown when <paramref name="requestedReviewerLogins"/> contains a null or
    /// blank login.
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
        // the property back to an array. The record element types are non-nullable,
        // so no element-null guard is needed; logins are strings, so blank ones are
        // reachable and rejected.
        RequestedReviewerLogins = SealLogins(
            requestedReviewerLogins,
            nameof(requestedReviewerLogins)
        );
        Reviews = Seal(reviews);
        Commits = Seal(commits);
        Comments = Seal(comments);
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

    private static IReadOnlyList<T> Seal<T>(IReadOnlyList<T> items) =>
        Array.AsReadOnly(items.ToArray());

    /// <summary>Gets the logins of directly requested reviewers (team-routed requests excluded).</summary>
    public IReadOnlyList<string> RequestedReviewerLogins { get; }

    /// <summary>Gets the submitted reviews.</summary>
    public IReadOnlyList<ReviewFact> Reviews { get; }

    /// <summary>Gets the commits that landed on the branch.</summary>
    public IReadOnlyList<CommitFact> Commits { get; }

    /// <summary>Gets the comments and replies.</summary>
    public IReadOnlyList<CommentFact> Comments { get; }
}
