namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// A shown pull request in the user's review queue: its identity, a last-updated
/// display stamp, and the three derived outputs (membership state, whether it has
/// an unseen update, and whether another reviewer already covered it). Hidden
/// pull requests do not produce a <see cref="QueueItem"/>. Ordering and grouping
/// of items are a presentation concern, not carried here.
/// </summary>
public sealed record QueueItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItem"/> class.
    /// </summary>
    /// <param name="identity">Where the pull request lives and how to display and link to it.</param>
    /// <param name="lastUpdatedBy">The login of whoever last updated the pull request.</param>
    /// <param name="lastUpdatedAt">The instant the pull request was last updated.</param>
    /// <param name="state">The shown membership state.</param>
    /// <param name="hasUpdate">Whether the pull request has an update the user has not seen.</param>
    /// <param name="isAlreadyCovered">Whether another reviewer has already submitted a review.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="lastUpdatedBy"/> is null, empty, or whitespace.
    /// </exception>
    public QueueItem(
        PullRequestIdentity identity,
        string lastUpdatedBy,
        DateTimeOffset lastUpdatedAt,
        MembershipState state,
        bool hasUpdate,
        bool isAlreadyCovered
    )
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastUpdatedBy);

        Identity = identity;
        LastUpdatedBy = lastUpdatedBy;
        LastUpdatedAt = lastUpdatedAt;
        State = state;
        HasUpdate = hasUpdate;
        IsAlreadyCovered = isAlreadyCovered;
    }

    /// <summary>Gets where the pull request lives and how to display and link to it.</summary>
    public PullRequestIdentity Identity { get; }

    /// <summary>Gets the login of whoever last updated the pull request.</summary>
    public string LastUpdatedBy { get; }

    /// <summary>Gets the instant the pull request was last updated.</summary>
    public DateTimeOffset LastUpdatedAt { get; }

    /// <summary>Gets the shown membership state.</summary>
    public MembershipState State { get; }

    /// <summary>Gets a value indicating whether the pull request has an update the user has not seen.</summary>
    public bool HasUpdate { get; }

    /// <summary>Gets a value indicating whether another reviewer has already submitted a review.</summary>
    public bool IsAlreadyCovered { get; }
}
