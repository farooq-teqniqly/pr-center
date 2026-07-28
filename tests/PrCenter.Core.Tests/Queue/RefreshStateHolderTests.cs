namespace PrCenter.Core.Tests.Queue;

using Microsoft.Extensions.Logging;
using PrCenter.Core.Queue;

public sealed class RefreshStateHolderTests
{
    private static readonly DateTimeOffset AttemptInstant = new(
        2026,
        7,
        14,
        8,
        0,
        0,
        TimeSpan.Zero
    );

    [Fact]
    public void Current_BeforeAnyRefresh_IsIdleAndNeverAttempted()
    {
        // Arrange
        var holder = Holder();

        // Act
        var current = holder.Current;

        // Assert
        Assert.False(current.InProgress);
        Assert.Null(current.LastAttemptAt);
        Assert.Null(current.Failure);
    }

    [Fact]
    public void BeginRefresh_ThenCurrent_IsInProgress()
    {
        // Arrange
        var holder = Holder();

        // Act
        holder.BeginRefresh();

        // Assert
        Assert.True(holder.Current.InProgress);
    }

    [Fact]
    public void BeginRefresh_KeepsThePreviousAttemptVisibleWhileTheNewOneRuns()
    {
        // Arrange
        var holder = Holder();
        holder.BeginRefresh();
        holder.CompleteRefresh("the earlier refresh failed");

        // Act
        holder.BeginRefresh();

        // Assert
        Assert.Equal(AttemptInstant, holder.Current.LastAttemptAt);
        Assert.Equal("the earlier refresh failed", holder.Current.Failure);
    }

    [Fact]
    public void CompleteRefresh_WithNoFailure_StampsTheAttemptAndClearsTheFailure()
    {
        // Arrange
        var holder = Holder();
        holder.BeginRefresh();
        holder.CompleteRefresh("an earlier failure");
        holder.BeginRefresh();

        // Act
        holder.CompleteRefresh(failure: null);

        // Assert
        Assert.False(holder.Current.InProgress);
        Assert.Equal(AttemptInstant, holder.Current.LastAttemptAt);
        Assert.Null(holder.Current.Failure);
    }

    [Fact]
    public void CompleteRefresh_WithAFailure_StampsTheAttemptAndRecordsTheFailure()
    {
        // Arrange
        var holder = Holder();
        holder.BeginRefresh();

        // Act
        holder.CompleteRefresh("The vault locked during the refresh.");

        // Assert
        Assert.False(holder.Current.InProgress);
        Assert.Equal(AttemptInstant, holder.Current.LastAttemptAt);
        Assert.Equal("The vault locked during the refresh.", holder.Current.Failure);
    }

    [Theory]
    [InlineData("begin")]
    [InlineData("complete")]
    public void RefreshStateHolder_OnEachTransition_RaisesChangedWithTheNewStateVisible(
        string transition
    )
    {
        // Arrange
        var holder = Holder();
        if (transition is "complete")
        {
            holder.BeginRefresh();
        }

        RefreshState? observed = null;
        holder.Changed += (_, _) => observed = holder.Current;

        // Act
        Apply(holder, transition);

        // Assert
        Assert.NotNull(observed);
        Assert.Equal(transition is "begin", observed.InProgress);
    }

    [Fact]
    public void RefreshStateHolder_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var holder = Holder();

        // Act
        holder.BeginRefresh();
        holder.CompleteRefresh(failure: null);

        // Assert
        Assert.Equal(AttemptInstant, holder.Current.LastAttemptAt);
    }

    [Fact]
    public void RefreshStateHolder_WhenASubscriberThrows_StillNotifiesTheOthersAndLogsAWarning()
    {
        // Arrange
        var logger = new CapturingLogger<RefreshStateHolder>();
        var holder = Holder(logger);
        var reachedSecondSubscriber = false;
        holder.Changed += (_, _) => throw new InvalidOperationException("subscriber faulted");
        holder.Changed += (_, _) => reachedSecondSubscriber = true;

        // Act
        holder.BeginRefresh();

        // Assert
        Assert.True(reachedSecondSubscriber);
        Assert.True(holder.Current.InProgress);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    private static void Apply(RefreshStateHolder holder, string transition)
    {
        if (transition is "begin")
        {
            holder.BeginRefresh();
            return;
        }

        holder.CompleteRefresh(failure: null);
    }

    private static RefreshStateHolder Holder(ILogger<RefreshStateHolder>? logger = null) =>
        new(
            new FixedTimeProvider(AttemptInstant),
            logger ?? new CapturingLogger<RefreshStateHolder>()
        );
}
