namespace PrCenter.Core.Tests.Queue;

using PrCenter.Core.Derivation;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

public sealed class GetQueueTests
{
    private static readonly DateTimeOffset Instant = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Execute_BeforeAnyPublish_ReturnsNull()
    {
        // Arrange
        var holder = new QueueSnapshotHolder(new FixedTimeProvider(Instant));
        var getQueue = new GetQueue(holder);

        // Act
        var snapshot = getQueue.Execute();

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public void Execute_AfterPublish_ReturnsLatestSnapshot()
    {
        // Arrange
        var holder = new QueueSnapshotHolder(new FixedTimeProvider(Instant));
        var getQueue = new GetQueue(holder);
        holder.Publish([], [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]);

        // Act
        var snapshot = getQueue.Execute();

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal(Instant, snapshot.SnapshotAt);
        Assert.Equal(OwnerFetchStatus.Ok, Assert.Single(snapshot.OwnerStatuses).Status);
    }
}
