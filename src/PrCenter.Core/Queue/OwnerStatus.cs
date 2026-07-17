using PrCenter.Core.Ports;

namespace PrCenter.Core.Queue;

/// <summary>
/// One owner's fetch outcome in a published <see cref="QueueSnapshot"/>: the
/// owner and the status of that owner's most recent fetch, with an optional
/// human-readable detail for the status indicator and, when the owner's rows are
/// carried over stale from a failed fetch, when they were last fresh. Immutable
/// data carrier.
/// </summary>
public sealed record OwnerStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerStatus"/> class.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the status is for.</param>
    /// <param name="status">The outcome of that owner's most recent fetch.</param>
    /// <param name="detail">An optional human-readable detail for the status indicator.</param>
    /// <param name="lastFreshAt">
    /// When this owner's carried-over rows were last fresh, or <see langword="null"/>
    /// when they are fresh as of this snapshot (an <see cref="OwnerFetchStatus.Ok"/>
    /// fetch, timestamped by the snapshot itself) or the owner has never been fresh.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null, empty, or whitespace.</exception>
    public OwnerStatus(
        string owner,
        OwnerFetchStatus status,
        string? detail = null,
        DateTimeOffset? lastFreshAt = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        Owner = owner;
        Status = status;
        Detail = detail;
        LastFreshAt = lastFreshAt;
    }

    /// <summary>Gets the GitHub owner (org or account) the status is for.</summary>
    public string Owner { get; }

    /// <summary>Gets the outcome of that owner's most recent fetch.</summary>
    public OwnerFetchStatus Status { get; }

    /// <summary>Gets an optional human-readable detail for the status indicator, or null.</summary>
    public string? Detail { get; }

    /// <summary>
    /// Gets when this owner's carried-over rows were last fresh, or
    /// <see langword="null"/> when they are fresh as of this snapshot or the owner
    /// has never been fresh.
    /// </summary>
    public DateTimeOffset? LastFreshAt { get; }
}
