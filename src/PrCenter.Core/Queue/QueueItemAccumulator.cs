using PrCenter.Core.Derivation;

namespace PrCenter.Core.Queue;

/// <summary>
/// Collects the queue items of a single refresh and resolves the pull requests
/// that more than one owner returned. Owner-queue discovery is not scoped to the
/// owner, so a token that can see another configured owner's repositories returns
/// that owner's pull requests too; without this the same pull request would be
/// published once per owner that saw it. A freshly fetched item wins over an item
/// carried over from a failed owner, because the carried item was derived from an
/// earlier poll's facts. Refresh-scoped machinery owned by <see cref="RefreshQueue"/>:
/// it is fed non-null collections from that one call site, so it takes no null guards.
/// </summary>
internal sealed class QueueItemAccumulator
{
    private readonly List<QueueItem> _fresh = [];
    private readonly List<QueueItem> _carriedOver = [];

    /// <summary>
    /// Adds the items an owner was fetched and derived fresh in this refresh.
    /// </summary>
    /// <param name="items">The freshly derived items.</param>
    public void AddFresh(IEnumerable<QueueItem> items) => _fresh.AddRange(items);

    /// <summary>
    /// Adds the items a failed owner carries forward from the previous snapshot.
    /// </summary>
    /// <param name="items">The carried-over items.</param>
    public void CarryOver(IEnumerable<QueueItem> items) => _carriedOver.AddRange(items);

    /// <summary>
    /// Gets the accumulated items with each pull request appearing once: the fresh
    /// items in the order their owners were polled, followed by the carried-over
    /// items of pull requests no fresh fetch produced.
    /// </summary>
    /// <returns>One item per distinct pull request.</returns>
    public IReadOnlyList<QueueItem> DistinctPullRequests()
    {
        // Fresh first so DistinctBy's keep-the-first rule resolves a collision in
        // favor of this poll's facts. Node ids are protocol values: ordinal.
        return _fresh
            .Concat(_carriedOver)
            .DistinctBy(item => item.Identity.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
