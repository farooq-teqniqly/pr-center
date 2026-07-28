namespace PrCenter.Core.Queue;

/// <summary>
/// The vault locked part-way through the refresh, so it was abandoned without
/// publishing: the previously published snapshot is left intact and is now stale.
/// </summary>
public sealed record RefreshAbortedByLock : RefreshOutcome
{
    /// <summary>Gets the single instance of this outcome, which carries no data.</summary>
    public static RefreshAbortedByLock Instance { get; } = new();
}
