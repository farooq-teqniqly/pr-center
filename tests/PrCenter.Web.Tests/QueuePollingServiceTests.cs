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
    private readonly RefreshStateHolder _refreshState;
    private readonly ServiceProvider _provider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public QueuePollingServiceTests()
    {
        StoredInterval(PollInterval.Default.Value);
        _refreshState = new RefreshStateHolder(_time, NullLogger<RefreshStateHolder>.Instance);

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

                return (RefreshOutcome)RefreshSucceeded.Instance;
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
    public async Task ExecuteAsync_WhileAPollIsRunning_PublishesTheRefreshAsInProgress()
    {
        // Arrange
        Unlocked();
        var polling = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        _refreshQueue
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                polling.TrySetResult();
                await release.Task;
                return (RefreshOutcome)RefreshSucceeded.Instance;
            });
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _trigger.RequestRefresh();
        await polling.Task.WaitAsync(Timeout, Ct);

        // Assert
        Assert.True(_refreshState.Current.InProgress);
        release.SetResult();
        await service.StopAsync(Ct);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAPollSucceeds_StampsTheInstantThePollFinished()
    {
        // Arrange: the clock moves while the poll runs, so a start-stamp and a
        // completion-stamp are distinguishable rather than coincidentally equal.
        Unlocked();
        var pollDuration = TimeSpan.FromSeconds(30);
        var startedAt = _time.GetUtcNow();
        var completed = new TaskCompletionSource();
        _refreshQueue
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _time.Advance(pollDuration);
                completed.TrySetResult();
                return Task.FromResult<RefreshOutcome>(RefreshSucceeded.Instance);
            });
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _trigger.RequestRefresh();
        await completed.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);

        // Assert
        Assert.False(_refreshState.Current.InProgress);
        Assert.Equal(startedAt + pollDuration, _refreshState.Current.LastCompletedAt);
        Assert.Null(_refreshState.Current.Failure);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheVaultLocksMidPoll_CompletesTheRefreshWithAFailure()
    {
        // Arrange
        Unlocked();
        var polled = SignalOnRefresh(RefreshAbortedByLock.Instance);
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _trigger.RequestRefresh();
        await polled.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);

        // Assert
        Assert.False(_refreshState.Current.InProgress);
        Assert.NotNull(_refreshState.Current.Failure);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAPollThrows_CompletesTheRefreshWithAFailureAndKeepsPolling()
    {
        // Arrange
        Unlocked();
        var calls = 0;
        var faulted = new TaskCompletionSource();
        var polledAgain = new TaskCompletionSource();
        _refreshQueue
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) is 1)
                {
                    faulted.TrySetResult();
                    throw new InvalidOperationException("poll blew up");
                }

                polledAgain.TrySetResult();
                return Task.FromResult<RefreshOutcome>(RefreshSucceeded.Instance);
            });
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _trigger.RequestRefresh();
        await faulted.Task.WaitAsync(Timeout, Ct);

        // Assert
        _trigger.RequestRefresh();
        await polledAgain.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
        Assert.False(_refreshState.Current.InProgress);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWakingWhileLocked_LeavesTheRefreshNeverCompleted()
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
        await lockChecked.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);

        // Assert
        Assert.False(_refreshState.Current.InProgress);
        Assert.Null(_refreshState.Current.LastCompletedAt);
    }

    [Theory]
    [InlineData("locked")]
    [InlineData("lock-read-throws")]
    public async Task ExecuteAsync_WhenAWakePollsNothing_StillNotifiesObserversItIsOver(string gate)
    {
        // Arrange: a consumed request that polls nothing must still publish, or a
        // caller holding a control closed against it waits for a signal that never
        // comes. Waited on the notification itself rather than on the gate, so
        // stopping the service cannot race the publish.
        var notified = new TaskCompletionSource();
        StubLockGate(gate);
        _refreshState.Changed += (_, _) => notified.TrySetResult();
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _trigger.RequestRefresh();
        await notified.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);

        // Assert
        Assert.False(_refreshState.Current.InProgress);
        Assert.Null(_refreshState.Current.LastCompletedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheLockStateReadThrows_KeepsTheLoopAliveForTheNextPoke()
    {
        // Arrange
        var calls = 0;
        var threw = new TaskCompletionSource();
        _appLock
            .GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) is 1)
                {
                    threw.TrySetResult();
                    throw new InvalidOperationException("lock state unreadable");
                }

                return AppLockState.Unlocked;
            });
        var polled = SignalOnRefresh();
        using var service = CreateService();
        await service.StartAsync(Ct);

        // Act
        _trigger.RequestRefresh();
        await threw.Task.WaitAsync(Timeout, Ct);

        // Assert
        _trigger.RequestRefresh();
        await polled.Task.WaitAsync(Timeout, Ct);
        await service.StopAsync(Ct);
        Assert.Null(_refreshState.Current.Failure);
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
    public async Task ExecuteAsync_WhenAPollTimesOut_StillPollsOnTheNextInterval()
    {
        // Arrange
        Unlocked();
        var timedOut = new TaskCompletionSource();
        var recovered = new TaskCompletionSource();
        var calls = 0;
        _refreshQueue
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    timedOut.TrySetResult();

                    // A request timeout surfaces as TaskCanceledException even though
                    // the service is not stopping -- the shape that must not be read
                    // as shutdown.
                    throw new TaskCanceledException("the GitHub request timed out");
                }

                recovered.TrySetResult();
                return Task.CompletedTask;
            });
        using var service = CreateService();
        await service.StartAsync(Ct);
        _time.Advance(PollInterval.Default.Value);
        await timedOut.Task.WaitAsync(Timeout, Ct);

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

    private void StubLockGate(string gate) =>
        _appLock
            .GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
                gate is "lock-read-throws"
                    ? throw new InvalidOperationException("lock state unreadable")
                    : AppLockState.Locked
            );

    private TaskCompletionSource SignalOnRefresh(RefreshOutcome? outcome = null)
    {
        var polled = new TaskCompletionSource();
        _refreshQueue
            .ExecuteAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                polled.TrySetResult();
                return Task.FromResult(outcome ?? RefreshSucceeded.Instance);
            });
        return polled;
    }

    private QueuePollingService CreateService() =>
        new(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            _trigger,
            _refreshState,
            _time,
            NullLogger<QueuePollingService>.Instance
        );

    public void Dispose() => _provider.Dispose();
}
