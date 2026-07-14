using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

namespace PrCenter.Web.Polling;

/// <summary>
/// Background service that drives the review-queue poll loop. The interval timer
/// and every on-demand refresh (manual refresh, unlock) poke the single refresh
/// trigger; the loop awaits that one trigger and, on each wake, polls only when
/// the app is Unlocked. Because the loop holds no trigger reader while a poll is
/// in flight, wakes that arrive mid-poll coalesce into at most one follow-up
/// poll, and the single loop guarantees polls never overlap. DI scoping for the
/// scoped ports is created per wake; the refresh use case is scope-agnostic.
/// </summary>
internal sealed class QueuePollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RefreshTrigger _trigger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _interval;
    private ITimer? _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuePollingService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The factory creating a DI scope per wake.</param>
    /// <param name="trigger">The refresh trigger the loop awaits and the timer pokes.</param>
    /// <param name="timeProvider">The clock backing the interval timer.</param>
    /// <param name="options">The poll options carrying the interval.</param>
    public QueuePollingService(
        IServiceScopeFactory scopeFactory,
        RefreshTrigger trigger,
        TimeProvider timeProvider,
        IOptions<PollingOptions> options
    )
    {
        _scopeFactory = scopeFactory;
        _trigger = trigger;
        _timeProvider = timeProvider;
        _interval = options.Value.Interval;
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // Start the interval timer before the loop so a tick is never missed while
        // the loop is spinning up: a poke that lands before the first wait buffers
        // in the trigger. The timer is just another poker of the one trigger.
        _timer = _timeProvider.CreateTimer(
            static state => ((RefreshTrigger)state!).RequestRefresh(),
            _trigger,
            _interval,
            _interval
        );
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _trigger.WaitForRequestAsync(stoppingToken).ConfigureAwait(false);
            await PollWhenUnlockedAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    private async Task PollWhenUnlockedAsync(CancellationToken cancellationToken)
    {
        // Gate on the app-lock state (the unlock UI gate), distinct from the vault
        // crypto lock that RefreshQueue guards against mid-poll. An async scope so
        // scoped IAsyncDisposable services (e.g. the EF Core context) dispose
        // asynchronously.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var appLock = scope.ServiceProvider.GetRequiredService<IAppLock>();
        if (
            await appLock.GetStateAsync(cancellationToken).ConfigureAwait(false)
            is not AppLockState.Unlocked
        )
        {
            return;
        }

        var refreshQueue = scope.ServiceProvider.GetRequiredService<IRefreshQueue>();
        await refreshQueue.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
