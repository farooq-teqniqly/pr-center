using PrCenter.Core.Derivation;

namespace PrCenter.Core.Queue;

/// <summary>
/// An immutable point-in-time view of the review queue: the derived queue items,
/// each polled owner's fetch status, and the instant the snapshot was taken.
/// Published by a refresh and read by observers; snapshots live in process memory
/// only. The absence of a snapshot (never polled since process start) is
/// represented by a null reference, distinct from a polled-but-empty snapshot.
/// </summary>
public sealed record QueueSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueSnapshot"/> class.
    /// </summary>
    /// <param name="items">The derived queue items across all polled owners.</param>
    /// <param name="ownerStatuses">Each polled owner's fetch status.</param>
    /// <param name="snapshotAt">The instant the snapshot was taken.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="items"/> or <paramref name="ownerStatuses"/> is null.
    /// </exception>
    public QueueSnapshot(
        IReadOnlyList<QueueItem> items,
        IReadOnlyList<OwnerStatus> ownerStatuses,
        DateTimeOffset snapshotAt
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(ownerStatuses);

        // Copy into read-only wrappers so the snapshot cannot be mutated through
        // the caller's reference after publication.
        Items = Array.AsReadOnly(items.ToArray());
        OwnerStatuses = Array.AsReadOnly(ownerStatuses.ToArray());
        SnapshotAt = snapshotAt;
    }

    /// <summary>Gets the derived queue items across all polled owners.</summary>
    public IReadOnlyList<QueueItem> Items { get; }

    /// <summary>Gets each polled owner's fetch status.</summary>
    public IReadOnlyList<OwnerStatus> OwnerStatuses { get; }

    /// <summary>Gets the instant the snapshot was taken.</summary>
    public DateTimeOffset SnapshotAt { get; }
}
