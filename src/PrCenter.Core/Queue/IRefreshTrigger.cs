namespace PrCenter.Core.Queue;

/// <summary>
/// Producer side of the single refresh trigger: a fire-and-forget request that
/// the poll loop should refresh promptly rather than waiting for its interval.
/// Manual refresh and a successful unlock poke this. Requests coalesce -- many
/// pokes while a poll is running or pending produce at most one subsequent poll.
/// </summary>
public interface IRefreshTrigger
{
    /// <summary>
    /// Requests an immediate poll. Returns without waiting; a pending request that
    /// has not yet been consumed absorbs the poke rather than queueing a second.
    /// </summary>
    void RequestRefresh();
}
