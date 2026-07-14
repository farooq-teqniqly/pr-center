namespace PrCenter.Core.Queue;

/// <summary>
/// Use case that runs one review-queue refresh: it enumerates the owners with a
/// stored token, derives the shown queue items relative to the user, and publishes
/// a new snapshot. A per-owner fetch failure degrades only that owner's status; a
/// locked vault mid-poll aborts the refresh, leaving the previous snapshot intact,
/// so a caller (the poll loop) needs no lock-specific handling of its own.
/// </summary>
public interface IRefreshQueue
{
    /// <summary>
    /// Runs one queue refresh and publishes the resulting snapshot, or leaves the
    /// previous snapshot untouched when the vault locks mid-poll.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the refresh has finished.</returns>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
