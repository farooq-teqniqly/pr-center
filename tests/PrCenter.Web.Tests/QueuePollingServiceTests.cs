namespace PrCenter.Web.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Web.Polling;

public sealed class QueuePollingServiceTests : IDisposable
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly IAppLock _appLock = Substitute.For<IAppLock>();
    private readonly IRefreshQueue _refreshQueue = Substitute.For<IRefreshQueue>();
    private readonly RefreshTrigger _trigger = new();
    private readonly FakeTimeProvider _time = new();
    private readonly ServiceProvider _provider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public QueuePollingServiceTests()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _appLock);
        services.AddScoped(_ => _refreshQueue);
        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteAsync_WhenIntervalElapsesWhileUnlocked_Polls()
    {
        // Arrange
        Unlocked();
        var polled = SignalOnRefresh();
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _time.Advance(DefaultInterval);

        // Assert
        await polled.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
        await _refreshQueue.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenWakingWhileLocked_DoesNotPoll()
    {
        // Arrange
        var lockChecked = new TaskCompletionSource();
        _appLock
            .GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lockChecked.TrySetResult();
                return AppLockState.Locked;
            });
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _time.Advance(DefaultInterval);

        // Assert
        await lockChecked.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
        await _refreshQueue.DidNotReceive().ExecuteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTriggerPoked_PollsWithoutWaitingForInterval()
    {
        // Arrange
        Unlocked();
        var polled = SignalOnRefresh();
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _trigger.RequestRefresh();

        // Assert
        await polled.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
        await _refreshQueue.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPokedDuringAnInFlightPoll_RunsAtMostOneFollowUpPollWithoutOverlap()
    {
        // Arrange
        Unlocked();
        var calls = 0;
        var firstStarted = new TaskCompletionSource();
        var secondStarted = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        _refreshQueue
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                switch (Interlocked.Increment(ref calls))
                {
                    case 1:
                        firstStarted.SetResult();
                        await release.Task;
                        break;
                    case 2:
                        secondStarted.SetResult();
                        break;
                }
            });
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _trigger.RequestRefresh();
        await firstStarted.Task.WaitAsync(Timeout, Ct);
        _trigger.RequestRefresh();
        _trigger.RequestRefresh();
        _trigger.RequestRefresh();
        release.SetResult();

        // Assert
        await secondStarted.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_WithConfiguredInterval_WaitsThatIntervalBetweenPolls()
    {
        // Arrange
        Unlocked();
        var interval = TimeSpan.FromMinutes(2);
        var polled = SignalOnRefresh();
        using var service = CreateService(interval);
        await service.StartAsync(Ct);

        // Act
        _time.Advance(interval - TimeSpan.FromSeconds(1));

        // Assert
        Assert.False(polled.Task.IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(1));
        await polled.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
    }

    private void Unlocked() =>
        _appLock.GetStateAsync(Arg.Any<CancellationToken>()).Returns(AppLockState.Unlocked);

    private TaskCompletionSource SignalOnRefresh()
    {
        var polled = new TaskCompletionSource();
        _refreshQueue
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                polled.TrySetResult();
                return Task.CompletedTask;
            });
        return polled;
    }

    private QueuePollingService CreateService(TimeSpan? interval = null) =>
        new(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _trigger,
            _time,
            Options.Create(new PollingOptions { Interval = interval ?? DefaultInterval })
        );

    public void Dispose() => _provider.Dispose();
}
