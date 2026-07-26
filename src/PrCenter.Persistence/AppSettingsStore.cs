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

        var stored = TimeSpan.FromSeconds(storedSeconds.Value);
        var clamped = PollInterval.Clamp(stored);
        if (clamped.Value != stored)
        {
            LogIntervalClamped(stored, clamped.Value);
        }

        return clamped;
    }

    /// <inheritdoc />
    public async Task SetPollIntervalAsync(
        PollInterval interval,
        CancellationToken cancellationToken = default
    )
    {
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
