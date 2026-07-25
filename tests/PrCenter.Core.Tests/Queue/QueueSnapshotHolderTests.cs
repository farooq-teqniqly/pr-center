namespace PrCenter.Core.Tests.Queue;

using Microsoft.Extensions.Logging;
using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

public sealed class QueueSnapshotHolderTests
{
    private static readonly DateTimeOffset PublishInstant = new(
        2026,
        7,
        14,
        8,
        0,
        0,
        TimeSpan.Zero
    );

    [Fact]
    public void Current_BeforeAnyPublish_IsNull()
    {
        // Arrange
        var holder = Holder();

        // Act
        var current = holder.Current;

        // Assert
        Assert.Null(current);
    }

    [Fact]
    public void Publish_ThenCurrent_ReturnsSnapshotStampedFromTimeProvider()
    {
        // Arrange
        var holder = Holder();
        var items = new[] { Item("pr-1") };
        var statuses = new[] { new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok) };

        // Act
        holder.Publish(items, statuses);

        // Assert
        var current = holder.Current;
        Assert.NotNull(current);
        Assert.Equal(PublishInstant, current.SnapshotAt);
        Assert.Equal(items, current.Items);
        Assert.Equal(statuses, current.OwnerStatuses);
    }

    [Fact]
    public void Publish_AfterAPreviousPublish_ReplacesTheSnapshotWhole()
    {
        // Arrange
        var holder = Holder();
        holder.Publish([Item("pr-old")], [new OwnerStatus("old-owner", OwnerFetchStatus.Error)]);
        var newItems = new[] { Item("pr-new") };
        var newStatuses = new[] { new OwnerStatus("new-owner", OwnerFetchStatus.Ok) };

        // Act
        holder.Publish(newItems, newStatuses);

        // Assert
        var current = holder.Current;
        Assert.NotNull(current);
        Assert.Equal(newItems, current.Items);
        Assert.Equal(newStatuses, current.OwnerStatuses);
    }

    [Fact]
    public void Publish_WithSubscriber_RaisesChangedWithTheNewSnapshotVisible()
    {
        // Arrange
        var holder = Holder();
        var items = new[] { Item("pr-1") };
        var statuses = new[] { new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok) };
        QueueSnapshot? observed = null;
        holder.Changed += (_, _) => observed = holder.Current;

        // Act
        holder.Publish(items, statuses);

        // Assert
        Assert.NotNull(observed);
        Assert.Equal(items, observed.Items);
    }

    [Fact]
    public void Publish_WithNoSubscribers_DoesNotThrowAndPublishesTheSnapshot()
    {
        // Arrange
        var holder = Holder();
        var items = new[] { Item("pr-1") };
        var statuses = new[] { new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok) };

        // Act
        var published = holder.Publish(items, statuses);

        // Assert
        Assert.Same(published, holder.Current);
    }

    [Fact]
    public void Publish_WhenASubscriberThrows_StillNotifiesTheOtherSubscribersAndLogsAWarning()
    {
        // Arrange
        var logger = new CapturingLogger<QueueSnapshotHolder>();
        var holder = Holder(logger);
        var reachedSecondSubscriber = false;
        holder.Changed += (_, _) => throw new InvalidOperationException("subscriber faulted");
        holder.Changed += (_, _) => reachedSecondSubscriber = true;

        // Act
        var published = holder.Publish(
            [Item("pr-1")],
            [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]
        );

        // Assert
        Assert.True(reachedSecondSubscriber);
        Assert.Same(published, holder.Current);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    private static QueueSnapshotHolder Holder(ILogger<QueueSnapshotHolder>? logger = null) =>
        new(
            new FixedTimeProvider(PublishInstant),
            logger ?? new CapturingLogger<QueueSnapshotHolder>()
        );

    private static QueueItem Item(string id) =>
        new(
            new PullRequestIdentity(
                id,
                "PerfectServe",
                "repo",
                1,
                "title",
                "https://example.test/pr",
                TestLogins.Author
            ),
            new LastUpdate("octocat", PublishInstant),
            MembershipState.AwaitingFirstReview,
            hasUpdate: false,
            roster: [],
            new MyEngagement(lastReviewedAt: null),
            coveredBy: []
        );
}
