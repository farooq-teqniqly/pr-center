namespace PrCenter.Core.Queue;

/// <summary>
/// The three collections one refresh accumulates as it walks its owners: the
/// queue items, the owner statuses the snapshot will carry, and the diagnostics
/// rows. They are one concept -- the refresh in progress -- so they travel
/// together rather than as parallel parameters on every per-owner call.
/// Refresh-scoped machinery owned by <see cref="RefreshQueue"/>: it is fed from
/// that one call site, so it takes no null guards.
/// </summary>
internal sealed class RefreshPass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshPass"/> class.
    /// </summary>
    /// <param name="diagnostics">The accumulator for this refresh's diagnostics rows.</param>
    public RefreshPass(PollDiagnosticsAccumulator diagnostics) => Diagnostics = diagnostics;

    /// <summary>Gets the accumulator resolving the items more than one owner returned.</summary>
    public QueueItemAccumulator Items { get; } = new();

    /// <summary>Gets the per-owner statuses the published snapshot will carry.</summary>
    public List<OwnerStatus> Statuses { get; } = [];

    /// <summary>Gets the accumulator for this refresh's diagnostics rows.</summary>
    public PollDiagnosticsAccumulator Diagnostics { get; }
}
