using PrCenter.Core.Ports;

namespace PrCenter.Core.Queue;

/// <summary>
/// One owner's fetch outcome in a published <see cref="QueueSnapshot"/>: the
/// owner and the status of that owner's most recent fetch, with an optional
/// human-readable detail for the status indicator. Immutable data carrier.
/// </summary>
public sealed record OwnerStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerStatus"/> class.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the status is for.</param>
    /// <param name="status">The outcome of that owner's most recent fetch.</param>
    /// <param name="detail">An optional human-readable detail for the status indicator.</param>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null, empty, or whitespace.</exception>
    public OwnerStatus(string owner, OwnerFetchStatus status, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        Owner = owner;
        Status = status;
        Detail = detail;
    }

    /// <summary>Gets the GitHub owner (org or account) the status is for.</summary>
    public string Owner { get; }

    /// <summary>Gets the outcome of that owner's most recent fetch.</summary>
    public OwnerFetchStatus Status { get; }

    /// <summary>Gets an optional human-readable detail for the status indicator, or null.</summary>
    public string? Detail { get; }
}
