namespace PrCenter.Core.Diagnostics;

/// <summary>
/// Which owner a diagnostics row is for and when the refresh worked on it.
/// Immutable data carrier with no behavior.
/// </summary>
public sealed record OwnerPollWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerPollWindow"/> class.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the row is for.</param>
    /// <param name="startedAt">When the refresh began this owner, or null when it never reached it.</param>
    /// <param name="completedAt">When the refresh finished this owner, or null when it never reached it.</param>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null, empty, or whitespace.</exception>
    public OwnerPollWindow(
        string owner,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        Owner = owner;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    /// <summary>Gets the GitHub owner (org or account) the row is for.</summary>
    public string Owner { get; }

    /// <summary>
    /// Gets when the refresh began this owner, or <see langword="null"/> when it
    /// never reached the owner. A null start instant is how a
    /// <see cref="Ports.OwnerFetchStatus.NotPolled"/> row says nothing was
    /// attempted.
    /// </summary>
    public DateTimeOffset? StartedAt { get; }

    /// <summary>Gets when the refresh finished this owner, or null when it never reached it.</summary>
    public DateTimeOffset? CompletedAt { get; }
}
