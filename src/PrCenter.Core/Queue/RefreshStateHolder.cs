using Microsoft.Extensions.Logging;

namespace PrCenter.Core.Queue;

/// <summary>
/// Process-wide holder for the poll loop's current <see cref="RefreshState"/>. Each
/// transition swaps in a new immutable state atomically; observers read the current
/// one. The poll loop is the sole writer, marking a refresh begun before it polls
/// and completed once it has finished, so the inbox can disable its refresh action
/// for the life of a poll and report how the last one ended.
/// </summary>
public sealed partial class RefreshStateHolder
{
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshStateHolder> _logger;
    private RefreshState _current = RefreshState.NeverRefreshed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshStateHolder"/> class.
    /// </summary>
    /// <param name="timeProvider">The clock used to stamp each refresh attempt.</param>
    /// <param name="logger">The logger for the faulted-subscriber warning path.</param>
    public RefreshStateHolder(TimeProvider timeProvider, ILogger<RefreshStateHolder> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current refresh state, never null: before the first attempt this is
    /// <see cref="RefreshState.NeverRefreshed"/>.
    /// </summary>
    public RefreshState Current => Volatile.Read(ref _current);

    /// <summary>
    /// Occurs after a transition has been published, so an observer can re-read
    /// <see cref="Current"/> rather than polling it. Raised after the reference swap,
    /// on the transitioning thread, so a handler reading <see cref="Current"/> always
    /// sees the just-published state; handlers must stay trivial and marshal any UI
    /// work off that thread. A faulting subscriber is logged and skipped rather than
    /// aborting the transition or the caller's poll loop.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Marks a refresh as started. The previous attempt's instant and failure stay
    /// visible while the new one runs, so the inbox keeps showing the last known
    /// outcome rather than blanking for the duration of the poll.
    /// </summary>
    public void BeginRefresh()
    {
        var current = Current;
        Publish(current with { InProgress = true });
    }

    /// <summary>
    /// Marks the running refresh as finished, stamping it with the current instant.
    /// </summary>
    /// <param name="failure">
    /// A description of how the refresh failed, or <see langword="null"/> when it
    /// succeeded. A per-owner fetch failure is not a refresh failure.
    /// </param>
    public void CompleteRefresh(string? failure) =>
        Publish(
            new RefreshState(
                InProgress: false,
                LastAttemptAt: _timeProvider.GetUtcNow(),
                Failure: failure
            )
        );

    private void Publish(RefreshState state)
    {
        Volatile.Write(ref _current, state);
        RaiseChanged();
    }

    // Each subscriber is invoked in its own try/catch for the same reason as
    // QueueSnapshotHolder: the publisher is a hosted background poll loop whose
    // default BackgroundServiceExceptionBehavior is StopHost, so an escaping handler
    // exception (e.g. a circuit torn down mid-transition) would stop the whole
    // process rather than degrade one browser tab.
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
