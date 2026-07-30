namespace PrCenter.Core.Derivation;

/// <summary>
/// A shown pull request's per-row derived status: its membership state, whether
/// it carries an update the user has not seen, and whether the user authored it.
/// Grouped as one carrier so <see cref="QueueItem"/> stays within the
/// constructor parameter limit. Immutable data carrier with no derivation
/// behavior; the authored-by-me flag is a display projection and never affects
/// membership.
/// </summary>
public sealed record QueueItemStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueItemStatus"/> class.
    /// </summary>
    /// <param name="state">The shown membership state.</param>
    /// <param name="hasUpdate">Whether the pull request has an update the user has not seen.</param>
    /// <param name="authoredByMe">Whether the pull request was authored by the user.</param>
    public QueueItemStatus(MembershipState state, bool hasUpdate, bool authoredByMe)
    {
        State = state;
        HasUpdate = hasUpdate;
        AuthoredByMe = authoredByMe;
    }

    /// <summary>Gets the shown membership state.</summary>
    public MembershipState State { get; }

    /// <summary>Gets a value indicating whether the pull request has an update the user has not seen.</summary>
    public bool HasUpdate { get; }

    /// <summary>Gets a value indicating whether the pull request was authored by the user.</summary>
    public bool AuthoredByMe { get; }
}
