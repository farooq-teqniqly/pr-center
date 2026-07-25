using Microsoft.Extensions.Logging;
using PrCenter.Core.Derivation;

namespace PrCenter.Core.Queue;

/// <summary>
/// Process-wide holder for the most recently published <see cref="QueueSnapshot"/>.
/// A refresh publishes a new snapshot via an atomic reference swap; observers read
/// the current one. Because a snapshot is immutable and only the reference changes,
/// a reader always observes either the old snapshot or the new one in full, never a
/// partial mixture. The holder starts empty (never polled) until the first publish.
/// </summary>
public sealed partial class QueueSnapshotHolder
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<QueueSnapshotHolder> _logger;
    private QueueSnapshot? _current;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueSnapshotHolder"/> class.
    /// </summary>
    /// <param name="timeProvider">The clock used to stamp each published snapshot.</param>
    /// <param name="logger">The logger for the faulted-subscriber warning path.</param>
    public QueueSnapshotHolder(TimeProvider timeProvider, ILogger<QueueSnapshotHolder> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the most recently published snapshot, or <see langword="null"/> when no
    /// refresh has published one since process start (the never-polled state).
    /// </summary>
    public QueueSnapshot? Current => Volatile.Read(ref _current);

    /// <summary>
    /// Occurs after a new snapshot has been published, so an observer can re-read
    /// <see cref="Current"/> without polling on a timer. Raised after the reference
    /// swap, on the publishing thread, so a handler reading <see cref="Current"/>
    /// always sees the just-published snapshot; handlers must stay trivial and
    /// marshal any UI work off that thread. <see cref="Publish"/> is the sole raise
    /// site, and it isolates each handler: a faulting subscriber is logged and
    /// skipped rather than aborting publication or the caller's poll loop.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Publishes a new snapshot from the given items and owner statuses, stamped
    /// with the current instant, replacing any previous snapshot atomically.
    /// </summary>
    /// <param name="items">The derived queue items across all polled owners.</param>
    /// <param name="ownerStatuses">Each polled owner's fetch status.</param>
    /// <returns>The snapshot that was published.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="items"/> or <paramref name="ownerStatuses"/> is null.
    /// </exception>
    public QueueSnapshot Publish(
        IReadOnlyList<QueueItem> items,
        IReadOnlyList<OwnerStatus> ownerStatuses
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(ownerStatuses);

        var snapshot = new QueueSnapshot(items, ownerStatuses, _timeProvider.GetUtcNow());
        Volatile.Write(ref _current, snapshot);
        RaiseChanged();
        return snapshot;
    }

    // Each subscriber is invoked in its own try/catch: the publisher is a hosted
    // background poll loop whose default BackgroundServiceExceptionBehavior is
    // StopHost, so an escaping handler exception (e.g. a circuit torn down mid-
    // publish) would stop the whole process rather than degrade one browser tab.
    private void RaiseChanged()
    {
        var handler = Changed;
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                subscriber(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogChangedSubscriberFaulted(ex);
            }
        }
    }
}
