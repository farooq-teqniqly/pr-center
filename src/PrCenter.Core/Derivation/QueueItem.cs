namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// A shown pull request in the user's review queue: its identity (including the
/// author), the last-updated display stamp, the derived membership state and
/// unseen-update flag, the reviewer roster, the user's own engagement, and the
/// other reviewers who already cover it. Hidden pull requests do not produce a
/// <see cref="QueueItem"/>. Ordering and grouping of items are a presentation
/// concern, not carried here.
/// </summary>
public sealed record QueueItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItem"/> class.
    /// </summary>
    /// <param name="identity">Where the pull request lives and how to display and link to it.</param>
    /// <param name="lastUpdate">The last-updated display stamp.</param>
    /// <param name="state">The shown membership state.</param>
    /// <param name="hasUpdate">Whether the pull request has an update the user has not seen.</param>
    /// <param name="roster">The reviewer roster: requested reviewers unioned with those who reviewed.</param>
    /// <param name="myEngagement">When the user last looked at and last reviewed the pull request.</param>
    /// <param name="coveredBy">The distinct other-human reviewers who already cover the pull request.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="identity"/>, <paramref name="lastUpdate"/>,
    /// <paramref name="roster"/>, <paramref name="myEngagement"/>, or
    /// <paramref name="coveredBy"/> is null.
    /// </exception>
    public QueueItem(
        PullRequestIdentity identity,
        LastUpdate lastUpdate,
        MembershipState state,
        bool hasUpdate,
        IReadOnlyList<ReviewerRosterEntry> roster,
        MyEngagement myEngagement,
        IReadOnlyList<string> coveredBy
    )
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(lastUpdate);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(myEngagement);
        ArgumentNullException.ThrowIfNull(coveredBy);

        Identity = identity;
        LastUpdate = lastUpdate;
        State = state;
        HasUpdate = hasUpdate;
        Roster = Seal(roster);
        MyEngagement = myEngagement;
        CoveredBy = Seal(coveredBy);
    }

    /// <summary>Gets where the pull request lives and how to display and link to it.</summary>
    public PullRequestIdentity Identity { get; }

    /// <summary>Gets the last-updated display stamp.</summary>
    public LastUpdate LastUpdate { get; }

    /// <summary>Gets the shown membership state.</summary>
    public MembershipState State { get; }

    /// <summary>Gets a value indicating whether the pull request has an update the user has not seen.</summary>
    public bool HasUpdate { get; }

    /// <summary>Gets the reviewer roster: requested reviewers unioned with those who reviewed.</summary>
    public IReadOnlyList<ReviewerRosterEntry> Roster { get; }

    /// <summary>Gets when the user last looked at and last reviewed the pull request.</summary>
    public MyEngagement MyEngagement { get; }

    /// <summary>Gets the distinct other-human reviewers who already cover the pull request.</summary>
    public IReadOnlyList<string> CoveredBy { get; }

    /// <summary>
    /// Gets a value indicating whether another reviewer already covers the pull
    /// request, derived from <see cref="CoveredBy"/> being non-empty.
    /// </summary>
    public bool IsAlreadyCovered => CoveredBy.Count > 0;

    // Copy into a read-only wrapper so the snapshot cannot be mutated -- neither
    // through the caller's original reference nor by casting the property back to
    // a mutable collection.
    private static IReadOnlyList<T> Seal<T>(IReadOnlyList<T> items) =>
        Array.AsReadOnly(items.ToArray());
}
