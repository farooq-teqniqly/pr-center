using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

namespace PrCenter.Core.Settings;

/// <summary>
/// Use case for changing the poll interval: stores it and pokes the refresh
/// trigger. The poke matters most when the interval is shortened -- without it
/// the new interval would not be felt until the sleep already in flight under
/// the old, longer one expired.
/// </summary>
public sealed class SavePollInterval
{
    private readonly IAppSettingsStore _store;
    private readonly IRefreshTrigger _trigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavePollInterval"/> class.
    /// </summary>
    /// <param name="store">The settings store persisting the interval.</param>
    /// <param name="trigger">The refresh trigger poked after a successful write.</param>
    public SavePollInterval(IAppSettingsStore store, IRefreshTrigger trigger)
    {
        _store = store;
        _trigger = trigger;
    }

    /// <summary>
    /// Stores the poll interval and pokes the refresh trigger on success. The
    /// parameter type carries the range invariant, so there is nothing left to
    /// validate here.
    /// </summary>
    /// <param name="interval">The interval between scheduled polls.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the interval is stored.</returns>
    public async Task SaveAsync(
        PollInterval interval,
        CancellationToken cancellationToken = default
    )
    {
        await _store.SetPollIntervalAsync(interval, cancellationToken).ConfigureAwait(false);
        _trigger.RequestRefresh();
    }
}
