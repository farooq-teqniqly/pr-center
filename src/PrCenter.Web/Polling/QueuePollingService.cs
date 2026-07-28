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
/// scoped ports is created per wake; the refresh use case is scope-agnostic. The
/// loop is the sole writer of the shared refresh state, marking each poll it
/// actually runs as started and then finished, and reporting a wake that polled
/// nothing as skipped, so the inbox can hold its refresh action closed for the
/// duration of a real poll, release it either way, and report how the last
/// refresh ended.
/// </summary>
internal sealed partial class QueuePollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RefreshTrigger _trigger;
    private readonly RefreshStateHolder _refreshState;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<QueuePollingService> _logger;
    private ITimer? _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuePollingService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The factory creating a DI scope per wake.</param>
    /// <param name="trigger">The refresh trigger the loop awaits and the timer pokes.</param>
    /// <param name="refreshState">The holder this loop publishes its refresh activity into.</param>
    /// <param name="timeProvider">The clock backing the interval timer.</param>
    /// <param name="logger">The logger for a cycle that faults.</param>
    public QueuePollingService(
        IServiceScopeFactory scopeFactory,
        RefreshTrigger trigger,
        RefreshStateHolder refreshState,
        TimeProvider timeProvider,
        ILogger<QueuePollingService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _trigger = trigger;
        _refreshState = refreshState;
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

        // A wake while the app is Locked polls nothing, so it is not a refresh:
        // gating before the state is marked keeps the inbox's last-refresh instant
        // pointing at the last real poll rather than at a no-op wake. The gate reads
        // storage, so it gets the same guard as the poll -- an exception escaping
        // here would end the loop for the life of the process. Either way the wake
        // is reported as skipped: it consumed a refresh request, and a caller
        // holding its own control closed against that request needs to hear that
        // nothing is coming.
        bool unlocked;
        try
        {
            unlocked = await IsUnlockedAsync(scope.ServiceProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogPollCycleFailed(ex);
            _refreshState.SkipRefresh();
            return;
        }

        if (!unlocked)
        {
            _refreshState.SkipRefresh();
            return;
        }

        // A faulting poll must not escape into ExecuteAsync's loop: an exception
        // there ends the BackgroundService for the life of the process, and the
        // still-armed timer would go on poking a trigger nobody awaits. Polling is
        // a repeating best-effort read, so one bad cycle is logged and skipped.
        // Only a cancellation of this service's own token means shutdown. A request
        // timeout also arrives as OperationCanceledException, so treating the type
        // alone as "we are stopping" would end the loop on a slow GitHub call --
        // matching RefreshQueue, which reads cancellation the same way.
        _refreshState.BeginRefresh();
        string? failure = null;
        try
        {
            var outcome = await PollAsync(scope.ServiceProvider, cancellationToken)
                .ConfigureAwait(false);
            failure = FailureFor(outcome);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogPollCycleFailed(ex);
            failure = FailureFor(ex);
        }
        finally
        {
            // In a finally so the state can never stick at in-progress -- a stuck
            // flag would leave the inbox's refresh action disabled for the life of
            // the process, including on the shutdown-cancellation path that
            // deliberately rethrows past the catch above.
            _refreshState.CompleteRefresh(failure);
        }
    }

    private static string? FailureFor(RefreshOutcome outcome) =>
        outcome is RefreshAbortedByLock
            ? "The vault locked during the refresh, so the queue below is stale."
            : null;

    // Transport-neutral wording, matching how RefreshQueue words a per-owner
    // failure: the user sees a timeout as a timeout and everything else as a
    // generic failure, with the exception itself left to the log.
    private static string FailureFor(Exception exception) =>
        exception is OperationCanceledException ? "The refresh timed out." : "The refresh failed.";

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
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogIntervalReadFailed(ex, PollInterval.Default.Value);
            return PollInterval.Default;
        }
    }

    // Gate on the app-lock state (the unlock UI gate), distinct from the vault
    // crypto lock that RefreshQueue guards against mid-poll.
    private static async Task<bool> IsUnlockedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        var appLock = services.GetRequiredService<IAppLock>();
        return await appLock.GetStateAsync(cancellationToken).ConfigureAwait(false)
            is AppLockState.Unlocked;
    }

    private static async Task<RefreshOutcome> PollAsync(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        var refreshQueue = services.GetRequiredService<IRefreshQueue>();
        return await refreshQueue.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
