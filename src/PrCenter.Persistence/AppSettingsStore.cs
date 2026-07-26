using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrCenter.Core.Ports;
using PrCenter.Core.Settings;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IAppSettingsStore"/> over the single
/// app-settings row. Touches no vault key and no encrypted column, so it works
/// in every lock state.
/// </summary>
internal sealed partial class AppSettingsStore : IAppSettingsStore
{
    private static readonly long MinSeconds = (long)PollInterval.Min.TotalSeconds;
    private static readonly long MaxSeconds = (long)PollInterval.Max.TotalSeconds;

    private readonly PrCenterDbContext _context;
    private readonly ILogger<AppSettingsStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppSettingsStore"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    /// <param name="logger">The logger for the clamped-value warning.</param>
    public AppSettingsStore(PrCenterDbContext context, ILogger<AppSettingsStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PollInterval> GetPollIntervalAsync(
        CancellationToken cancellationToken = default
    )
    {
        var storedSeconds = await _context
            .AppSettings.AsNoTracking()
            .Where(setting => setting.Id == AppSetting.SingletonId)
            .Select(setting => (long?)setting.PollIntervalSeconds)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (storedSeconds is null)
        {
            return PollInterval.Default;
        }

        // Clamp in the column's own units before building a TimeSpan: the column is
        // a long, and TimeSpan.FromSeconds throws for a magnitude a long can hold
        // but a TimeSpan cannot. Converting first would put that throw ahead of the
        // clamp, which is the one thing this read path must never do.
        var storedInRange = Math.Clamp(storedSeconds.Value, MinSeconds, MaxSeconds);
        if (storedInRange != storedSeconds.Value)
        {
            LogIntervalClamped(storedSeconds.Value, storedInRange);
        }

        return new PollInterval(TimeSpan.FromSeconds(storedInRange));
    }

    /// <inheritdoc />
    public async Task SetPollIntervalAsync(
        PollInterval interval,
        CancellationToken cancellationToken = default
    )
    {
        // The parameter type carries the range invariant everywhere except one
        // hole: default(PollInterval) skips the constructor and is a zero
        // interval. Re-check here so the port's promise that an out-of-range
        // value cannot reach storage is actually true of every caller.
        ArgumentOutOfRangeException.ThrowIfLessThan(interval.Value, PollInterval.Min);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(interval.Value, PollInterval.Max);

        var seconds = (long)interval.Value.TotalSeconds;

        // Tracked read: this row is about to be written, so the change tracker is
        // doing its job here rather than being paid for nothing.
        var existing = await _context
            .AppSettings.FirstOrDefaultAsync(
                setting => setting.Id == AppSetting.SingletonId,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing is null)
        {
            _context.AppSettings.Add(
                new AppSetting { Id = AppSetting.SingletonId, PollIntervalSeconds = seconds }
            );
        }
        else
        {
            existing.PollIntervalSeconds = seconds;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
