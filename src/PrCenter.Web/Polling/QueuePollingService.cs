using Microsoft.Extensions.DependencyInjection;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;

namespace PrCenter.Web.Polling;

/// <summary>
/// Background service that drives the review-queue poll loop. The interval timer
/// and every on-demand refresh (manual refresh, unlock) poke the single refresh
/// trigger; the loop awaits that one trigger and, on each wake, polls only when
/// the app is Unlocked. Because the loop holds no trigger reader while a poll is
/// in flight, wakes that arrive mid-poll coalesce into at most one follow-up
/// poll, and the single loop guarantees polls never overlap. The timer is
/// one-shot and re-armed on every wake from the stored interval, so an interval
/// edited in the app takes effect on the next cycle without a restart, and every
/// wake -- timer or on-demand -- restarts the interval clock. DI scoping for the
/// scoped ports is created per wake; the refresh use case is scope-agnostic.
/// </summary>
internal sealed partial class QueuePollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RefreshTrigger _trigger;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<QueuePollingService> _logger;
    private ITimer? _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuePollingService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The factory creating a DI scope per wake.</param>
    /// <param name="trigger">The refresh trigger the loop awaits and the timer pokes.</param>
    /// <param name="timeProvider">The clock backing the interval timer.</param>
    /// <param name="logger">The logger for a cycle that faults.</param>
    public QueuePollingService(
        IServiceScopeFactory scopeFactory,
        RefreshTrigger trigger,
        TimeProvider timeProvider,
        ILogger<QueuePollingService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _trigger = trigger;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Arm before the loop so a tick is never missed while the loop is spinning
        // up: a poke that lands before the first wait buffers in the trigger. The
        // timer is just another poker of the one trigger.
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var interval = await ReadIntervalAsync(scope.ServiceProvider, cancellationToken)
                .ConfigureAwait(false);
            _timer = _timeProvider.CreateTimer(
                static state => ((RefreshTrigger)state!).RequestRefresh(),
                _trigger,
                interval.Value,
                System.Threading.Timeout.InfiniteTimeSpan
            );
        }

        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _trigger.WaitForRequestAsync(stoppingToken).ConfigureAwait(false);
            await RunCycleAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        // One async scope per wake so scoped IAsyncDisposable services (e.g. the
        // EF Core context) dispose asynchronously. The timer is re-armed before
        // the poll runs, so the next tick is scheduled even if the poll faults.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var interval = await ReadIntervalAsync(scope.ServiceProvider, cancellationToken)
            .ConfigureAwait(false);
        _timer?.Change(interval.Value, System.Threading.Timeout.InfiniteTimeSpan);

        // A faulting poll must not escape into ExecuteAsync's loop: an exception
        // there ends the BackgroundService for the life of the process, and the
        // still-armed timer would go on poking a trigger nobody awaits. Polling is
        // a repeating best-effort read, so one bad cycle is logged and skipped.
        try
        {
            await PollWhenUnlockedAsync(scope.ServiceProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPollCycleFailed(ex);
        }
    }

    private async Task<PollInterval> ReadIntervalAsync(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        // The interval read gates the re-arm, so its failure is the more dangerous
        // one: without a fallback there is no next tick at all. The default keeps
        // the loop alive at a sane cadence until the stored row is readable again.
        try
        {
            return await services
                .GetRequiredService<IAppSettingsStore>()
                .GetPollIntervalAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogIntervalReadFailed(ex, PollInterval.Default.Value);
            return PollInterval.Default;
        }
    }

    private static async Task PollWhenUnlockedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        // Gate on the app-lock state (the unlock UI gate), distinct from the vault
        // crypto lock that RefreshQueue guards against mid-poll.
        var appLock = services.GetRequiredService<IAppLock>();
        if (
            await appLock.GetStateAsync(cancellationToken).ConfigureAwait(false)
            is not AppLockState.Unlocked
        )
        {
            return;
        }

        var refreshQueue = services.GetRequiredService<IRefreshQueue>();
        await refreshQueue.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
