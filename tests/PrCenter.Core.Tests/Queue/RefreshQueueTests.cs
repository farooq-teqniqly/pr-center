namespace PrCenter.Core.Tests.Queue;

using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Facts;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

public sealed class RefreshQueueTests
{
    private static readonly DateTimeOffset Instant = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    private readonly ITokenVault _vault = Substitute.For<ITokenVault>();
    private readonly IGitHubFacts _facts = Substitute.For<IGitHubFacts>();
    private readonly IStateStore _stateStore = Substitute.For<IStateStore>();
    private readonly QueueSnapshotHolder _holder = new(new FixedTimeProvider(Instant));
    private readonly CapturingLogger<RefreshQueue> _logger = new();

    [Fact]
    public async Task ExecuteAsync_WithMultipleOwners_PublishesEveryOwnersItemsWithOkStatuses()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe", "ps-unite"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        StubOwner("ps-unite", ShownFact("ps-unite", "ps-unite/repo#1"));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = _holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal(
            ["PerfectServe/repo#1", "ps-unite/repo#1"],
            snapshot.Items.Select(item => item.Identity.Id).OrderBy(id => id)
        );
        Assert.All(
            snapshot.OwnerStatuses,
            status => Assert.Equal(OwnerFetchStatus.Ok, status.Status)
        );
        await _facts
            .Received(1)
            .GetAuthenticatedUserLoginAsync("PerfectServe", Arg.Any<CancellationToken>());
        await _facts
            .Received(1)
            .GetAuthenticatedUserLoginAsync("ps-unite", Arg.Any<CancellationToken>());
        await _stateStore
            .Received(1)
            .GetLastSeenAsync("PerfectServe/repo#1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneOwnerFetchFails_DegradesOnlyThatOwner()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["good", "bad"]);
        StubOwner("good", ShownFact("good", "good/repo#1"));
        _facts
            .GetAuthenticatedUserLoginAsync("bad", Arg.Any<CancellationToken>())
            .Returns(TestLogins.Me);
        _facts
            .GetReviewQueueFactsAsync("bad", TestLogins.Me, Arg.Any<CancellationToken>())
            .Returns(new OwnerFactsResult(OwnerFetchStatus.Error, [], "boom"));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = _holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal("good/repo#1", Assert.Single(snapshot.Items).Identity.Id);
        var badStatus = Assert.Single(snapshot.OwnerStatuses, status => status.Owner == "bad");
        Assert.Equal(OwnerFetchStatus.Error, badStatus.Status);
        Assert.Equal("boom", badStatus.Detail);
        Assert.DoesNotContain(snapshot.Items, item => item.Identity.Owner == "bad");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneOwnerFetchThrows_DegradesOnlyThatOwnerWithoutCrashing()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["good", "bad"]);
        StubOwner("good", ShownFact("good", "good/repo#1"));
        _facts
            .GetAuthenticatedUserLoginAsync("bad", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No token is configured for owner 'bad'."));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = _holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal("good/repo#1", Assert.Single(snapshot.Items).Identity.Id);
        var badStatus = Assert.Single(snapshot.OwnerStatuses, status => status.Owner == "bad");
        Assert.Equal(OwnerFetchStatus.Error, badStatus.Status);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoOwners_PublishesEmptySnapshotWithoutCallingGitHub()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = _holder.Current;
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Items);
        Assert.Empty(snapshot.OwnerStatuses);
        await _facts
            .DidNotReceive()
            .GetAuthenticatedUserLoginAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _facts
            .DidNotReceive()
            .GetReviewQueueFactsAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_WhenVaultLocksMidPoll_AbortsLogsWarningAndLeavesSnapshot()
    {
        // Arrange
        var previous = _holder.Publish([], []);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        _facts
            .GetAuthenticatedUserLoginAsync("PerfectServe", Arg.Any<CancellationToken>())
            .ThrowsAsync(new VaultLockedException());

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Same(previous, _holder.Current);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerFetchFails_CarriesPreviousItemsMarkedStale()
    {
        // Arrange -- first poll fresh, second poll the owner errors
        var clock = new AdvanceableTimeProvider(Instant);
        var holder = new QueueSnapshotHolder(clock);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        clock.Now = Instant.AddMinutes(5);
        StubOwnerError("PerfectServe", "boom");

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal("PerfectServe/repo#1", Assert.Single(snapshot.Items).Identity.Id);
        var status = Assert.Single(snapshot.OwnerStatuses);
        Assert.Equal(OwnerFetchStatus.Error, status.Status);
        Assert.Equal(Instant, status.LastFreshAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerFetchThrows_CarriesPreviousItemsMarkedStale()
    {
        // Arrange -- first poll fresh, second poll login resolution throws
        var clock = new AdvanceableTimeProvider(Instant);
        var holder = new QueueSnapshotHolder(clock);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        clock.Now = Instant.AddMinutes(5);
        _facts
            .GetAuthenticatedUserLoginAsync("PerfectServe", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("No token is configured."));

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal("PerfectServe/repo#1", Assert.Single(snapshot.Items).Identity.Id);
        var status = Assert.Single(snapshot.OwnerStatuses);
        Assert.Equal(OwnerFetchStatus.Error, status.Status);
        Assert.Equal(Instant, status.LastFreshAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerFailsConsecutively_ChainsTheOriginalFreshInstant()
    {
        // Arrange
        var clock = new AdvanceableTimeProvider(Instant);
        var holder = new QueueSnapshotHolder(clock);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        StubOwnerError("PerfectServe", "boom");
        clock.Now = Instant.AddMinutes(5);
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        clock.Now = Instant.AddMinutes(10);

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert -- still the original fresh instant, not the intervening failed snapshot's
        Assert.Equal(Instant, Assert.Single(holder.Current!.OwnerStatuses).LastFreshAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerRecovers_PublishesFreshItemsWithNullLastFreshAt()
    {
        // Arrange
        var clock = new AdvanceableTimeProvider(Instant);
        var holder = new QueueSnapshotHolder(clock);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        clock.Now = Instant.AddMinutes(5);
        StubOwnerError("PerfectServe", "boom");
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        clock.Now = Instant.AddMinutes(10);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert
        var status = Assert.Single(holder.Current!.OwnerStatuses);
        Assert.Equal(OwnerFetchStatus.Ok, status.Status);
        Assert.Null(status.LastFreshAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerFailsOnFirstPoll_HasStatusOnlyWithNoItemsAndNullInstant()
    {
        // Arrange
        var holder = new QueueSnapshotHolder(new FixedTimeProvider(Instant));
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwnerError("PerfectServe", "boom");

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = holder.Current;
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Items);
        var status = Assert.Single(snapshot.OwnerStatuses);
        Assert.Equal(OwnerFetchStatus.Error, status.Status);
        Assert.Null(status.LastFreshAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerRemovedFromVault_DropsItsCarriedItems()
    {
        // Arrange -- both owners fresh, then one is removed from the vault
        var holder = new QueueSnapshotHolder(new FixedTimeProvider(Instant));
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["keep", "drop"]);
        StubOwner("keep", ShownFact("keep", "keep/repo#1"));
        StubOwner("drop", ShownFact("drop", "drop/repo#1"));
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["keep"]);

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal("keep/repo#1", Assert.Single(snapshot.Items).Identity.Id);
        Assert.DoesNotContain(snapshot.OwnerStatuses, status => status.Owner == "drop");
    }

    private RefreshQueue CreateRefreshQueue() => new(_vault, _facts, _stateStore, _holder, _logger);

    private RefreshQueue RefreshQueueWith(QueueSnapshotHolder holder) =>
        new(_vault, _facts, _stateStore, holder, _logger);

    private void StubOwnerError(string owner, string detail)
    {
        _facts
            .GetAuthenticatedUserLoginAsync(owner, Arg.Any<CancellationToken>())
            .Returns(TestLogins.Me);
        _facts
            .GetReviewQueueFactsAsync(owner, TestLogins.Me, Arg.Any<CancellationToken>())
            .Returns(new OwnerFactsResult(OwnerFetchStatus.Error, [], detail));
    }

    private sealed class AdvanceableTimeProvider : TimeProvider
    {
        public AdvanceableTimeProvider(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private void StubOwner(string owner, PullRequestFacts facts)
    {
        _facts
            .GetAuthenticatedUserLoginAsync(owner, Arg.Any<CancellationToken>())
            .Returns(TestLogins.Me);
        _facts
            .GetReviewQueueFactsAsync(owner, TestLogins.Me, Arg.Any<CancellationToken>())
            .Returns(new OwnerFactsResult(OwnerFetchStatus.Ok, [facts]));
    }

    private static PullRequestFacts ShownFact(string owner, string id) =>
        new(
            new PullRequestIdentity(
                id,
                owner,
                "repo",
                1,
                "title",
                $"https://github.com/{owner}/repo/pull/1",
                TestLogins.Author
            ),
            new PullRequestStatus(
                isDraft: false,
                isClosedOrMerged: false,
                lastUpdatedBy: "author",
                lastUpdatedAt: Instant
            ),
            new PullRequestActivity([TestLogins.Me], [], [], [])
        );
}
