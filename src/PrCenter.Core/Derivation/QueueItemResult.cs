namespace PrCenter.Core.Derivation;

/// <summary>
/// The outcome of deriving a pull request's queue item: either shown with an
/// <see cref="Item"/>, or hidden with the <see cref="Exclusion"/> that hid it.
/// The factory methods keep the two cases consistent so an invalid combination
/// cannot form. The exclusion is reporting only -- no membership, update, or
/// covered decision reads it.
/// </summary>
public sealed record QueueItemResult
{
    private QueueItemResult(bool isShown, QueueItem? item, MembershipExclusion? exclusion)
    {
        IsShown = isShown;
        Item = item;
        Exclusion = exclusion;
    }

    /// <summary>Gets a value indicating whether the pull request is shown in the queue.</summary>
    public bool IsShown { get; }

    /// <summary>Gets the derived queue item, or <see langword="null"/> when the pull request is hidden.</summary>
    public QueueItem? Item { get; }

    /// <summary>Gets the reason the pull request is hidden, or <see langword="null"/> when it is shown.</summary>
    public MembershipExclusion? Exclusion { get; }

    /// <summary>
    /// Creates a shown result carrying the derived queue item.
    /// </summary>
    /// <param name="item">The queue item derived for the shown pull request.</param>
    /// <returns>A shown <see cref="QueueItemResult"/>.</returns>
    public static QueueItemResult Shown(QueueItem item) => new(true, item, null);

    /// <summary>
    /// Creates a hidden result with the given exclusion reason.
    /// </summary>
    /// <param name="exclusion">The reason the pull request is hidden.</param>
    /// <returns>A hidden <see cref="QueueItemResult"/>.</returns>
    public static QueueItemResult Hidden(MembershipExclusion exclusion) =>
        new(false, null, exclusion);
}
