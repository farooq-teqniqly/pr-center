namespace PrCenter.Web.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;
using PrCenter.Web.Polling;

public sealed class QueuePollingServiceTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    // A poll runs on the loop's thread, so "did not poll" has to be waited out
    // rather than read off the task the instant the fake clock advances.
    private static readonly TimeSpan SettleWindow = TimeSpan.FromMilliseconds(500);

    private readonly IAppLock _appLock = Substitute.For<IAppLock>();
    private readonly IRefreshQueue _refreshQueue = Substitute.For<IRefreshQueue>();
    private readonly IAppSettingsStore _settings = Substitute.For<IAppSettingsStore>();
    private readonly RefreshTrigger _trigger = new();
    private readonly FakeTimeProvider _time = new();
    private readonly ServiceProvider _provider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public QueuePollingServiceTests()
    {
        StoredInterval(PollInterval.Default.Value);

        var services = new ServiceCollection();
        services.AddScoped(_ => _appLock);
        services.AddScoped(_ => _refreshQueue);
        services.AddScoped(_ => _settings);
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
        _time.Advance(PollInterval.Default.Value);

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
        _time.Advance(PollInterval.Default.Value);

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
    public async Task StartAsync_WithAStoredInterval_ArmsTheFirstPollAtThatInterval()
    {
        // Arrange
        Unlocked();
        var stored = TimeSpan.FromMinutes(30);
        StoredInterval(stored);
        var polled = SignalOnRefresh();
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _time.Advance(stored - OneSecond);

        // Assert
        await AssertNoPollAsync(polled);
        _time.Advance(OneSecond);
        await polled.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
    }

    [Fact]
    public async Task StartAsync_WithNoStoredInterval_ArmsTheFirstPollAtTheDefaultInterval()
    {
        // Arrange
        Unlocked();
        var polled = SignalOnRefresh();
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _time.Advance(PollInterval.Default.Value - OneSecond);

        // Assert
        await AssertNoPollAsync(polled);
        _time.Advance(OneSecond);
        await polled.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheStoredIntervalChangesMidRun_ReArmsWithTheNewValue()
    {
        // Arrange
        Unlocked();
        var original = TimeSpan.FromMinutes(10);
        var replacement = TimeSpan.FromMinutes(20);
        StoredInterval(original);
        var firstPoll = SignalOnRefresh();
        using var service = CreateService();
        await service.StartAsync(Ct);
        StoredInterval(replacement);
        _time.Advance(original);
        await firstPoll.Task.WaitAsync(Timeout, Ct);
        var secondPoll = SignalOnRefresh();

        // Act
        _time.Advance(original);

        // Assert
        await AssertNoPollAsync(secondPoll);
        _time.Advance(replacement - original);
        await secondPoll.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTriggerPoked_RestartsTheIntervalFromThatPoll()
    {
        // Arrange
        Unlocked();
        var interval = TimeSpan.FromMinutes(10);
        var half = TimeSpan.FromMinutes(5);
        StoredInterval(interval);
        var firstPoll = SignalOnRefresh();
        using var service = CreateService();
        await service.StartAsync(Ct);
        _time.Advance(half);
        _trigger.RequestRefresh();
        await firstPoll.Task.WaitAsync(Timeout, Ct);
        var secondPoll = SignalOnRefresh();

        // Act
        _time.Advance(half);

        // Assert
        await AssertNoPollAsync(secondPoll);
        _time.Advance(half);
        await secondPoll.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAPollFaults_StillPollsOnTheNextInterval()
    {
        // Arrange
        Unlocked();
        var faulted = new TaskCompletionSource();
        var recovered = new TaskCompletionSource();
        var calls = 0;
        _refreshQueue
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    faulted.TrySetResult();
                    throw new InvalidOperationException("the fetch blew up");
                }

                recovered.TrySetResult();
                return Task.CompletedTask;
            });
        using var service = CreateService();
        await service.StartAsync(Ct);
        _time.Advance(PollInterval.Default.Value);
        await faulted.Task.WaitAsync(Timeout, Ct);

        // Act
        _time.Advance(PollInterval.Default.Value);

        // Assert
        await recovered.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheIntervalReadFaults_FallsBackToTheDefaultAndKeepsPolling()
    {
        // Arrange
        Unlocked();
        var reads = 0;
        _settings
            .GetPollIntervalAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
                Interlocked.Increment(ref reads) == 2
                    ? throw new InvalidOperationException("the settings row is unreadable")
                    : PollInterval.Default
            );
        var firstPoll = SignalOnRefresh();
        using var service = CreateService();
        await service.StartAsync(Ct);
        _time.Advance(PollInterval.Default.Value);
        await firstPoll.Task.WaitAsync(Timeout, Ct);
        var secondPoll = SignalOnRefresh();

        // Act
        _time.Advance(PollInterval.Default.Value);

        // Assert
        await secondPoll.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
        await _refreshQueue.Received(2).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    private static async Task AssertNoPollAsync(TaskCompletionSource polled) =>
        await Assert.ThrowsAsync<TimeoutException>(() => polled.Task.WaitAsync(SettleWindow, Ct));

    private void StoredInterval(TimeSpan interval) =>
        _settings
            .GetPollIntervalAsync(Arg.Any<CancellationToken>())
            .Returns(new PollInterval(interval));

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

    private QueuePollingService CreateService() =>
        new(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _trigger,
            _time,
            NullLogger<QueuePollingService>.Instance
        );

    public void Dispose() => _provider.Dispose();
}
