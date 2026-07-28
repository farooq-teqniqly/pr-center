namespace PrCenter.Core.Queue;

/// <summary>
/// The refresh ran to completion and published a new snapshot. Owners whose fetch
/// failed are carried in that snapshot as degraded statuses; the refresh itself
/// still succeeded.
/// </summary>
public sealed record RefreshSucceeded : RefreshOutcome
{
    /// <summary>Gets the single instance of this outcome, which carries no data.</summary>
    public static RefreshSucceeded Instance { get; } = new();
}
