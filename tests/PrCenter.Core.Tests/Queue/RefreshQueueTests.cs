namespace PrCenter.Core.Tests.Queue;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Facts;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

public sealed class RefreshQueueTests
{
    private static readonly DateTimeOffset Instant = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    private readonly ITokenVault _vault = Substitute.For<ITokenVault>();
    private readonly IGitHubFacts _facts = Substitute.For<IGitHubFacts>();
    private readonly QueueSnapshotHolder _holder = new(
        new FixedTimeProvider(Instant),
        NullLogger<QueueSnapshotHolder>.Instance
    );
    private readonly CapturingLogger<RefreshQueue> _logger = new();
    private readonly RecordingPollDiagnosticsSink _sink = new();

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
    public async Task ExecuteAsync_WhenTwoOwnersReturnTheSamePullRequest_PublishesOneItem()
    {
        // Arrange -- one PAT can see another configured owner's repositories, so
        // the same pull request comes back under both owners
        _vault
            .ListOwnersAsync(Arg.Any<CancellationToken>())
            .Returns(["PerfectServe", "ps-unite"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        StubOwner("ps-unite", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = _holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal("PerfectServe/repo#1", Assert.Single(snapshot.Items).Identity.Id);
        Assert.Equal(
            ["PerfectServe", "ps-unite"],
            snapshot.OwnerStatuses.Select(status => status.Owner)
        );
        Assert.All(
            snapshot.OwnerStatuses,
            status => Assert.Equal(OwnerFetchStatus.Ok, status.Status)
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenAStaleCarryOverCollidesWithAFreshItem_KeepsTheFreshItem()
    {
        // Arrange -- the failing owner is enumerated first, so its carried-over row
        // reaches the accumulator before the healthy owner's fresh copy
        var clock = new AdvanceableTimeProvider(Instant);
        var holder = new QueueSnapshotHolder(clock, NullLogger<QueueSnapshotHolder>.Instance);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe", "ps-unite"]);
        StubOwner("PerfectServe", TitledFact("PerfectServe", "PerfectServe/repo#1", "stale title"));
        StubOwner("ps-unite", TitledFact("PerfectServe", "PerfectServe/repo#1", "stale title"));
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        clock.Now = Instant.AddMinutes(5);
        StubOwnerError("PerfectServe", "boom");
        StubOwner("ps-unite", TitledFact("PerfectServe", "PerfectServe/repo#1", "fresh title"));

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal("fresh title", Assert.Single(snapshot.Items).Identity.Title);
        var staleStatus = Assert.Single(
            snapshot.OwnerStatuses,
            status => status.Owner == "PerfectServe"
        );
        Assert.Equal(OwnerFetchStatus.Error, staleStatus.Status);
        Assert.Equal(Instant, staleStatus.LastFreshAt);
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
    public async Task ExecuteAsync_WhenVaultLocksMidPoll_ReturnsAbortedByLock()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        _facts
            .GetAuthenticatedUserLoginAsync("PerfectServe", Arg.Any<CancellationToken>())
            .ThrowsAsync(new VaultLockedException());

        // Act
        var outcome = await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.IsType<RefreshAbortedByLock>(outcome);
    }

    [Fact]
    public async Task ExecuteAsync_WhenASnapshotIsPublished_ReturnsSucceeded()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "pr-1"));

        // Act
        var outcome = await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.IsType<RefreshSucceeded>(outcome);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneOwnerFetchFails_StillReturnsSucceeded()
    {
        // Arrange: a per-owner failure degrades that owner, it does not fail the refresh.
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        _facts
            .GetAuthenticatedUserLoginAsync("PerfectServe", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no token"));

        // Act
        var outcome = await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.IsType<RefreshSucceeded>(outcome);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerFetchFails_CarriesPreviousItemsMarkedStale()
    {
        // Arrange -- first poll fresh, second poll the owner errors
        var clock = new AdvanceableTimeProvider(Instant);
        var holder = new QueueSnapshotHolder(clock, NullLogger<QueueSnapshotHolder>.Instance);
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
        var holder = new QueueSnapshotHolder(clock, NullLogger<QueueSnapshotHolder>.Instance);
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
        var holder = new QueueSnapshotHolder(clock, NullLogger<QueueSnapshotHolder>.Instance);
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
        var holder = new QueueSnapshotHolder(clock, NullLogger<QueueSnapshotHolder>.Instance);
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
        var holder = new QueueSnapshotHolder(
            new FixedTimeProvider(Instant),
            NullLogger<QueueSnapshotHolder>.Instance
        );
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
        var holder = new QueueSnapshotHolder(
            new FixedTimeProvider(Instant),
            NullLogger<QueueSnapshotHolder>.Instance
        );
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

    [Fact]
    public async Task ExecuteAsync_WhenOwnerCasingDiffersBetweenPolls_CarriesStaleItemsAndFreshInstant()
    {
        // Arrange -- first poll fresh as "PerfectServe", second poll the vault reports the same owner lower-cased and errors
        const string freshOwner = "PerfectServe";
        var relistedOwner = freshOwner.ToLowerInvariant();
        var clock = new AdvanceableTimeProvider(Instant);
        var holder = new QueueSnapshotHolder(clock, NullLogger<QueueSnapshotHolder>.Instance);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns([freshOwner]);
        StubOwner(freshOwner, ShownFact(freshOwner, $"{freshOwner}/repo#1"));
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        clock.Now = Instant.AddMinutes(5);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns([relistedOwner]);
        StubOwnerError(relistedOwner, "boom");

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert
        var snapshot = holder.Current;
        Assert.NotNull(snapshot);
        Assert.Equal($"{freshOwner}/repo#1", Assert.Single(snapshot.Items).Identity.Id);
        var status = Assert.Single(snapshot.OwnerStatuses);
        Assert.Equal(OwnerFetchStatus.Error, status.Status);
        Assert.Equal(Instant, status.LastFreshAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshSucceeds_WritesExactlyOneRecord()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var record = Assert.Single(_sink.Records);
        Assert.Equal(PollOutcome.Succeeded, record.Run.Outcome);
        Assert.Equal(Instant, record.Run.StartedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshSucceeds_WritesOneOwnerRowPerConfiguredOwner()
    {
        // Arrange
        _vault
            .ListOwnersAsync(Arg.Any<CancellationToken>())
            .Returns(["PerfectServe", "ps-unite", "farooq"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        StubOwner("ps-unite", ShownFact("ps-unite", "ps-unite/repo#1"));
        StubOwner("farooq", ShownFact("farooq", "farooq/repo#1"));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var record = Assert.Single(_sink.Records);
        Assert.Equal(
            ["PerfectServe", "ps-unite", "farooq"],
            record.Owners.Select(row => row.Window.Owner)
        );
        Assert.Equal(["PerfectServe", "ps-unite", "farooq"], record.Run.ConfiguredOwners);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshSucceeds_RecordsThePublishedItemCount()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner(
            "PerfectServe",
            [
                ShownFact("PerfectServe", "PerfectServe/repo#1"),
                ShownFact("PerfectServe", "PerfectServe/repo#2"),
            ]
        );

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var record = Assert.Single(_sink.Records);
        Assert.Equal(_holder.Current!.Items.Count, record.Run.PublishedCount);
        Assert.Equal(2, record.Run.PublishedCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshSucceeds_RecordsPerOwnerFetchCounts()
    {
        // Arrange -- one shown pull request and one draft, from a search pair that
        // returned four nodes before the union
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner(
            "PerfectServe",
            [
                ShownFact("PerfectServe", "PerfectServe/repo#1"),
                DraftFact("PerfectServe", "PerfectServe/repo#2"),
            ],
            new FetchDiagnostics(RequestedCount: 3, ReviewedCount: 1, RateLimit: null)
        );

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var row = Assert.Single(Assert.Single(_sink.Records).Owners);
        Assert.Equal(3, row.Counts!.Requested);
        Assert.Equal(1, row.Counts.Reviewed);
        Assert.Equal(2, row.Counts.Union);
        Assert.Equal(1, row.Counts.Derived);
        Assert.Equal(0, row.Counts.CarriedOver);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRefreshSucceeds_RecordsPerOwnerExclusionCountsAndRateLimit()
    {
        // Arrange
        var rateLimit = new RateLimitReading(4987, Instant.AddHours(1), 13);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner(
            "PerfectServe",
            [
                ShownFact("PerfectServe", "PerfectServe/repo#1"),
                DraftFact("PerfectServe", "PerfectServe/repo#2"),
            ],
            new FetchDiagnostics(2, 0, rateLimit)
        );

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var row = Assert.Single(Assert.Single(_sink.Records).Owners);
        Assert.Equal(1, row.Exclusions!.Draft);
        Assert.Equal(1, row.Exclusions.Total);
        Assert.Equal(rateLimit, row.RateLimit);
        Assert.Equal(TestLogins.Me, row.Outcome.ResolvedLogin);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnOwnerFails_RecordsItsCarryOverCountWithNoFetchCounts()
    {
        // Arrange -- fresh first, then the owner errors so its rows carry over
        var clock = new AdvanceableTimeProvider(Instant);
        var holder = new QueueSnapshotHolder(clock, NullLogger<QueueSnapshotHolder>.Instance);
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);
        clock.Now = Instant.AddMinutes(5);
        StubOwnerError("PerfectServe", "boom");

        // Act
        await RefreshQueueWith(holder).ExecuteAsync(CancellationToken.None);

        // Assert
        var row = Assert.Single(_sink.Records[^1].Owners);
        Assert.Equal(1, row.Counts!.CarriedOver);
        Assert.Null(row.Counts.Union);
        Assert.Null(row.Exclusions);
        Assert.Equal(OwnerFetchStatus.Error, row.Outcome.Status);
        Assert.Equal("boom", row.Outcome.Detail);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTwoOwnersReturnTheSamePullRequest_RecordsItUnderBoth()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe", "ps-unite"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        StubOwner("ps-unite", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var record = Assert.Single(_sink.Records);
        Assert.All(
            record.Owners,
            row => Assert.Equal(["PerfectServe/repo#1"], row.Contributed.Ids)
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenTwoOwnersReturnTheSamePullRequest_SumsDerivedAbovePublished()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe", "ps-unite"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        StubOwner("ps-unite", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert -- the overlap is visible only as this difference
        var record = Assert.Single(_sink.Records);
        Assert.Equal(2, record.Owners.Sum(row => row.Counts!.Derived ?? 0));
        Assert.Equal(1, record.Run.PublishedCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnOwnerReachesIntoAnother_RecordsTheForeignItemCount()
    {
        // Arrange -- one token sees the other configured owner's repository
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe", "ps-unite"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));
        StubOwner("ps-unite", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var record = Assert.Single(_sink.Records);
        Assert.Equal(0, Row(record, "PerfectServe").Contributed.ForeignCount);
        Assert.Equal(1, Row(record, "ps-unite").Contributed.ForeignCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenVaultLocksMidPoll_WritesAnAbortedRecordWithNotPolledRows()
    {
        // Arrange -- the first owner locks, so the second is never reached
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["locks", "unreached"]);
        _facts
            .GetAuthenticatedUserLoginAsync("locks", Arg.Any<CancellationToken>())
            .ThrowsAsync(new VaultLockedException());

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert
        var record = Assert.Single(_sink.Records);
        Assert.Equal(PollOutcome.AbortedByLock, record.Run.Outcome);
        Assert.Null(record.Run.PublishedCount);
        var unreached = Row(record, "unreached");
        Assert.Equal(OwnerFetchStatus.NotPolled, unreached.Outcome.Status);
        Assert.Null(unreached.Window.StartedAt);
        Assert.Null(unreached.Counts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenShutdownCancels_WritesACanceledRecord()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        _facts
            .GetAuthenticatedUserLoginAsync("PerfectServe", Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateRefreshQueue().ExecuteAsync(cts.Token)
        );

        // Assert
        var record = Assert.Single(_sink.Records);
        Assert.Equal(PollOutcome.Canceled, record.Run.Outcome);
        Assert.Null(record.Run.PublishedCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerEnumerationThrows_WritesAFaultedRecordWithNoOwners()
    {
        // Arrange
        _vault
            .ListOwnersAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("the vault is unreadable"));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRefreshQueue().ExecuteAsync(CancellationToken.None)
        );

        // Assert -- absent owners, not zero: a broken vault must not read as an empty one
        var record = Assert.Single(_sink.Records);
        Assert.Equal(PollOutcome.Faulted, record.Run.Outcome);
        Assert.Null(record.Run.ConfiguredOwners);
        Assert.Empty(record.Owners);
        Assert.Null(record.Run.PublishedCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoOwners_WritesARecordWithNoOwnerRowsAndAnEmptyOwnerList()
    {
        // Arrange
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        await CreateRefreshQueue().ExecuteAsync(CancellationToken.None);

        // Assert -- an empty list is a real configuration, distinct from an unread one
        var record = Assert.Single(_sink.Records);
        Assert.Equal(PollOutcome.Succeeded, record.Run.Outcome);
        Assert.NotNull(record.Run.ConfiguredOwners);
        Assert.Empty(record.Run.ConfiguredOwners);
        Assert.Empty(record.Owners);
        Assert.Equal(0, record.Run.PublishedCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenASinkThrows_StillSucceedsAndLogsAWarning()
    {
        // Arrange
        var throwing = new RecordingPollDiagnosticsSink(new InvalidOperationException("sink down"));
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        var outcome = await RefreshQueueWith(_holder, throwing)
            .ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.IsType<RefreshSucceeded>(outcome);
        Assert.NotNull(_holder.Current);
        Assert.Contains(_logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheFirstSinkThrows_StillWritesToTheSecond()
    {
        // Arrange
        var throwing = new RecordingPollDiagnosticsSink(new InvalidOperationException("sink down"));
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        await RefreshQueueWith(_holder, throwing, _sink).ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.Single(_sink.Records);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheFirstSinkSpendsTheWholeWriteBudget_StillWritesToTheSecond()
    {
        // Arrange -- the first sink takes longer than the budget the second is promised
        var clock = new FakeTimeProvider(Instant);
        var slow = new BudgetSpendingPollDiagnosticsSink(clock, TimeSpan.FromSeconds(5));
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        StubOwner("PerfectServe", ShownFact("PerfectServe", "PerfectServe/repo#1"));

        // Act
        await new RefreshQueue(_vault, _facts, _holder, _logger, [slow, _sink], clock).ExecuteAsync(
            CancellationToken.None
        );

        // Assert
        Assert.Single(_sink.Records);
        Assert.False(_sink.WriteWasCanceled);
    }

    [Fact]
    public async Task ExecuteAsync_WhenASinkThrows_DoesNotReplaceThePropagatingException()
    {
        // Arrange
        var throwing = new RecordingPollDiagnosticsSink(new InvalidOperationException("sink down"));
        _vault
            .ListOwnersAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("the vault is unreadable"));

        // Act
        var exception = await Assert.ThrowsAsync<IOException>(() =>
            RefreshQueueWith(_holder, throwing).ExecuteAsync(CancellationToken.None)
        );

        // Assert -- the refresh's own failure, not the sink's
        Assert.Equal("the vault is unreadable", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCallersTokenIsAlreadyCanceled_StillWritesTheRecord()
    {
        // Arrange -- the shutdown path is exactly the one the write must survive
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _vault.ListOwnersAsync(Arg.Any<CancellationToken>()).Returns(["PerfectServe"]);
        _facts
            .GetAuthenticatedUserLoginAsync("PerfectServe", Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateRefreshQueue().ExecuteAsync(cts.Token)
        );

        // Assert
        Assert.Single(_sink.Records);
        Assert.False(_sink.WriteWasCanceled);
    }

    private static OwnerPollDiagnostics Row(PollDiagnostics record, string owner) =>
        Assert.Single(record.Owners, row => row.Window.Owner == owner);

    private RefreshQueue CreateRefreshQueue() => RefreshQueueWith(_holder);

    private RefreshQueue RefreshQueueWith(QueueSnapshotHolder holder) =>
        RefreshQueueWith(holder, _sink);

    private RefreshQueue RefreshQueueWith(
        QueueSnapshotHolder holder,
        params IPollDiagnosticsSink[] sinks
    ) => new(_vault, _facts, holder, _logger, sinks, new FixedTimeProvider(Instant));

    private void StubOwnerError(string owner, string detail)
    {
        _facts
            .GetAuthenticatedUserLoginAsync(owner, Arg.Any<CancellationToken>())
            .Returns(TestLogins.Me);
        _facts
            .GetReviewQueueFactsAsync(owner, TestLogins.Me, Arg.Any<CancellationToken>())
            .Returns(new OwnerFactsResult(OwnerFetchStatus.Error, [], detail));
    }

    private void StubOwner(string owner, PullRequestFacts facts) => StubOwner(owner, [facts]);

    private void StubOwner(
        string owner,
        IReadOnlyList<PullRequestFacts> facts,
        FetchDiagnostics? diagnostics = null
    )
    {
        _facts
            .GetAuthenticatedUserLoginAsync(owner, Arg.Any<CancellationToken>())
            .Returns(TestLogins.Me);
        _facts
            .GetReviewQueueFactsAsync(owner, TestLogins.Me, Arg.Any<CancellationToken>())
            .Returns(new OwnerFactsResult(OwnerFetchStatus.Ok, facts, null, diagnostics));
    }

    private static PullRequestFacts ShownFact(string owner, string id) =>
        TitledFact(owner, id, "title");

    private static PullRequestFacts DraftFact(string owner, string id) =>
        new(
            new PullRequestIdentity(
                id,
                owner,
                "repo",
                2,
                "draft",
                $"https://github.com/{owner}/repo/pull/2",
                TestLogins.Author
            ),
            new PullRequestStatus(
                isDraft: true,
                isClosedOrMerged: false,
                lastUpdatedBy: "author",
                lastUpdatedAt: Instant
            ),
            new PullRequestActivity([TestLogins.Me], [], [], [])
        );

    private static PullRequestFacts TitledFact(string owner, string id, string title) =>
        new(
            new PullRequestIdentity(
                id,
                owner,
                "repo",
                1,
                title,
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

    private sealed class AdvanceableTimeProvider : TimeProvider
    {
        public AdvanceableTimeProvider(DateTimeOffset now) => Now = now;

        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
