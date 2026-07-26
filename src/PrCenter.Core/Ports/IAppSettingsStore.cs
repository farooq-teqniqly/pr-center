using PrCenter.Core.Settings;

namespace PrCenter.Core.Ports;

/// <summary>
/// Port for the stored application settings. The poll interval is the only
/// setting; it lives in the database rather than in application configuration so
/// it is editable from inside the app. Nothing here is secret: reads and writes
/// need no vault key and work in every lock state, which is what lets the poll
/// loop read the interval before it knows whether the app is unlocked.
/// </summary>
public interface IAppSettingsStore
{
    /// <summary>
    /// Reads the stored poll interval, or <see cref="PollInterval.Default"/> when
    /// no interval has been stored. A stored value outside the allowed range is
    /// clamped into range rather than throwing, so a value edited outside the app
    /// degrades to a usable interval instead of making the app unbootable.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stored interval, the default when none is stored, or the clamped value.</returns>
    Task<PollInterval> GetPollIntervalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the poll interval, replacing any previously stored value. The
    /// parameter type constrains the value to the allowed range, so an
    /// out-of-range interval cannot reach storage through this port. The
    /// interval is stored at whole-second resolution: any sub-second component
    /// is dropped and does not round-trip. This is a deliberate property of the
    /// contract rather than an implementation detail -- a poll cadence measured
    /// in minutes has no use for finer precision, and whole seconds keep the
    /// stored value readable and repairable in a SQLite client.
    /// </summary>
    /// <param name="interval">The interval between scheduled polls.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the interval is stored.</returns>
    Task SetPollIntervalAsync(PollInterval interval, CancellationToken cancellationToken = default);
}
