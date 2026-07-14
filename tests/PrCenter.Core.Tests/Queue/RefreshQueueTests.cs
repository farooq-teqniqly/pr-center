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

    private RefreshQueue CreateRefreshQueue() => new(_vault, _facts, _stateStore, _holder, _logger);

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
                $"https://github.com/{owner}/repo/pull/1"
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
