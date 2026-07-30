using PrCenter.Core.Diagnostics;
using PrCenter.Core.Facts;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

namespace PrCenter.Core.Tests.Queue;

public sealed class PollDiagnosticsAccumulatorTests
{
    private static readonly Guid PollId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset StartedAt = new(2026, 7, 29, 14, 5, 0, TimeSpan.Zero);

    [Fact]
    public void Build_ConfiguredOwners_AreThoseHandedInAtConstruction()
    {
        // Arrange
        var accumulator = Accumulator("acme", "ps-unite", "farooq");
        RecordPolled(accumulator, "acme");

        // Act
        var record = accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 1);

        // Assert -- the never-recorded owners are still accounted for, because the
        // list does not come from the rows it is meant to check
        Assert.Equal(["acme", "ps-unite", "farooq"], record.Run.ConfiguredOwners);
    }

    [Fact]
    public void Build_WithoutRecordingAnOwner_StillListsItAsConfigured()
    {
        // Arrange
        var accumulator = Accumulator("acme", "never-recorded");
        RecordPolled(accumulator, "acme");

        // Act
        var record = accumulator.Build(Completed(), PollOutcome.Faulted, publishedCount: null);

        // Assert
        Assert.Contains("never-recorded", record.Run.ConfiguredOwners!);
        Assert.DoesNotContain(record.Owners, row => row.Window.Owner == "never-recorded");
    }

    [Fact]
    public void Build_WhenOwnerContributesOnlyItsOwnPullRequests_ReportsNoForeignItems()
    {
        // Arrange
        var accumulator = Accumulator("acme");
        accumulator.RecordPolled(
            Window("acme"),
            new OwnerPollOutcome(OwnerFetchStatus.Ok, resolvedLogin: "octocat"),
            FetchCounts.Fetched(requested: 2, reviewed: 1, union: 2, derived: 2),
            new ExclusionCounts(0, 0, 0, 0),
            rateLimit: null,
            [Identity("acme", "api", 12), Identity("acme", "api", 19)]
        );

        // Act
        var row = Assert.Single(
            accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 2).Owners
        );

        // Assert
        Assert.Equal(0, row.Contributed.ForeignCount);
        Assert.Equal(["acme/api#12", "acme/api#19"], row.Contributed.Ids);
    }

    [Fact]
    public void Build_WhenOwnerContributesAnotherOwnersPullRequests_CountsThemAsForeign()
    {
        // Arrange
        var accumulator = Accumulator("acme");
        accumulator.RecordPolled(
            Window("acme"),
            new OwnerPollOutcome(OwnerFetchStatus.Ok),
            FetchCounts.Fetched(requested: 3, reviewed: 0, union: 3, derived: 3),
            new ExclusionCounts(0, 0, 0, 0),
            rateLimit: null,
            [
                Identity("acme", "api", 12),
                Identity("ps-unite", "tools", 3),
                Identity("farooq", "pr-center", 42),
            ]
        );

        // Act
        var row = Assert.Single(
            accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 3).Owners
        );

        // Assert
        Assert.Equal(2, row.Contributed.ForeignCount);
    }

    [Fact]
    public void Build_WhenOwnerWasPolled_CapturesItsCounts()
    {
        // Arrange
        var accumulator = Accumulator("acme");
        var rateLimit = new RateLimitReading(4987, Completed(), 13);
        accumulator.RecordPolled(
            Window("acme"),
            new OwnerPollOutcome(OwnerFetchStatus.Ok, resolvedLogin: "octocat"),
            FetchCounts.Fetched(requested: 12, reviewed: 8, union: 15, derived: 4),
            new ExclusionCounts(Draft: 6, ClosedOrMerged: 2, Approved: 3, Untracked: 0),
            rateLimit,
            [Identity("acme", "api", 12)]
        );

        // Act
        var row = Assert.Single(
            accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 4).Owners
        );

        // Assert
        Assert.Equal(new FetchCounts(12, 8, 15, 4, 0), row.Counts);
        Assert.Equal(new ExclusionCounts(6, 2, 3, 0), row.Exclusions);
        Assert.Equal(rateLimit, row.RateLimit);
        Assert.Equal("octocat", row.Outcome.ResolvedLogin);
        Assert.Equal(StartedAt, row.Window.StartedAt);
    }

    [Fact]
    public void Build_WhenOwnerCarriedOver_CapturesCarryOverCountWithNullFetchCounts()
    {
        // Arrange
        var accumulator = Accumulator("acme");
        accumulator.RecordCarriedOver(
            Window("acme"),
            new OwnerPollOutcome(OwnerFetchStatus.MisconfiguredToken, "The token was rejected."),
            carriedOverCount: 5,
            [Identity("acme", "api", 12)]
        );

        // Act
        var row = Assert.Single(
            accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 5).Owners
        );

        // Assert
        Assert.NotNull(row.Counts);
        Assert.Equal(5, row.Counts.CarriedOver);
        Assert.Null(row.Counts.Requested);
        Assert.Null(row.Counts.Reviewed);
        Assert.Null(row.Counts.Union);
        Assert.Null(row.Counts.Derived);
        Assert.Null(row.Exclusions);
    }

    [Fact]
    public void Build_WhenOwnerNeverCarriedAnything_DistinguishesZeroFromAbsent()
    {
        // Arrange
        var accumulator = Accumulator("acme");
        accumulator.RecordCarriedOver(
            Window("acme"),
            new OwnerPollOutcome(OwnerFetchStatus.Error, "The owner's queue could not be fetched."),
            carriedOverCount: 0,
            []
        );

        // Act
        var row = Assert.Single(
            accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 0).Owners
        );

        // Assert -- zero carried is a claim; a NotPolled row's absent count is not
        Assert.NotNull(row.Counts);
        Assert.Equal(0, row.Counts.CarriedOver);
    }

    [Fact]
    public void MarkRemainingUnreached_ProducesNotPolledRowsWithNullStartInstants()
    {
        // Arrange
        var accumulator = Accumulator("acme", "ps-unite", "farooq");
        RecordPolled(accumulator, "acme");

        // Act
        accumulator.MarkRemainingUnreached();
        var record = accumulator.Build(
            Completed(),
            PollOutcome.AbortedByLock,
            publishedCount: null
        );

        // Assert
        var unreached = record.Owners.Where(row =>
            row.Outcome.Status == OwnerFetchStatus.NotPolled
        );
        Assert.Equal(["ps-unite", "farooq"], unreached.Select(row => row.Window.Owner));
        Assert.All(
            unreached,
            row =>
            {
                Assert.Null(row.Window.StartedAt);
                Assert.Null(row.Counts);
                Assert.Null(row.Exclusions);
                Assert.Empty(row.Contributed.Ids);
            }
        );
    }

    [Fact]
    public void MarkRemainingUnreached_DoesNotDisturbAlreadyRecordedOwners()
    {
        // Arrange
        var accumulator = Accumulator("acme", "ps-unite");
        RecordPolled(accumulator, "acme");

        // Act
        accumulator.MarkRemainingUnreached();
        var record = accumulator.Build(
            Completed(),
            PollOutcome.AbortedByLock,
            publishedCount: null
        );

        // Assert
        var polled = record.Owners.Single(row => row.Window.Owner == "acme");
        Assert.Equal(OwnerFetchStatus.Ok, polled.Outcome.Status);
    }

    [Fact]
    public void Build_AfterMarkingUnreached_HasOneRowPerConfiguredOwner()
    {
        // Arrange
        var accumulator = Accumulator("acme", "ps-unite", "farooq");
        RecordPolled(accumulator, "ps-unite");

        // Act
        accumulator.MarkRemainingUnreached();
        var record = accumulator.Build(Completed(), PollOutcome.Canceled, publishedCount: null);

        // Assert -- in configured order, so the abort point reads off the rows
        Assert.Equal(["acme", "ps-unite", "farooq"], record.Owners.Select(row => row.Window.Owner));
    }

    [Fact]
    public void Build_WithNoConfiguredOwners_HasNoRowsAndAnEmptyConfiguredList()
    {
        // Arrange
        var accumulator = Accumulator();

        // Act
        accumulator.MarkRemainingUnreached();
        var record = accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 0);

        // Assert -- an empty list is a real configuration, not an unread one
        Assert.Empty(record.Owners);
        Assert.NotNull(record.Run.ConfiguredOwners);
        Assert.Empty(record.Run.ConfiguredOwners);
    }

    [Fact]
    public void Build_WhenAnUnconfiguredOwnerWasRecorded_KeepsItsRow()
    {
        // Arrange
        var accumulator = Accumulator("acme");
        RecordPolled(accumulator, "unexpected");

        // Act
        accumulator.MarkRemainingUnreached();
        var record = accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 1);

        // Assert -- the disagreement is the point: dropping the row would hide it
        Assert.Contains(record.Owners, row => row.Window.Owner == "unexpected");
        Assert.Contains(record.Owners, row => row.Window.Owner == "acme");
    }

    [Fact]
    public void Build_ForAPolledOwner_ExclusionCountsPlusDerivedEqualTheUnionCount()
    {
        // Arrange
        var accumulator = Accumulator("acme");
        accumulator.RecordPolled(
            Window("acme"),
            new OwnerPollOutcome(OwnerFetchStatus.Ok),
            FetchCounts.Fetched(requested: 12, reviewed: 8, union: 15, derived: 4),
            new ExclusionCounts(Draft: 6, ClosedOrMerged: 2, Approved: 3, Untracked: 0),
            rateLimit: null,
            []
        );

        // Act
        var row = Assert.Single(
            accumulator.Build(Completed(), PollOutcome.Succeeded, publishedCount: 4).Owners
        );

        // Assert
        Assert.Equal(row.Counts!.Union, row.Counts.Derived + row.Exclusions!.Total);
    }

    [Fact]
    public void Build_CarriesThePollIdentityAndWindow()
    {
        // Arrange
        var accumulator = Accumulator("acme");
        var completedAt = Completed();

        // Act
        var record = accumulator.Build(completedAt, PollOutcome.Succeeded, publishedCount: 0);

        // Assert
        Assert.Equal(PollId, record.Run.PollId);
        Assert.Equal(StartedAt, record.Run.StartedAt);
        Assert.Equal(completedAt, record.Run.CompletedAt);
        Assert.Equal(PollOutcome.Succeeded, record.Run.Outcome);
        Assert.Equal(0, record.Run.PublishedCount);
    }

    [Fact]
    public void Build_WhenNothingWasPublished_LeavesThePublishedCountAbsent()
    {
        // Arrange
        var accumulator = Accumulator("acme");

        // Act
        var record = accumulator.Build(Completed(), PollOutcome.Faulted, publishedCount: null);

        // Assert
        Assert.Null(record.Run.PublishedCount);
    }

    private static PollDiagnosticsAccumulator Accumulator(params string[] configuredOwners) =>
        new(PollId, StartedAt, configuredOwners);

    private static void RecordPolled(PollDiagnosticsAccumulator accumulator, string owner) =>
        accumulator.RecordPolled(
            Window(owner),
            new OwnerPollOutcome(OwnerFetchStatus.Ok),
            FetchCounts.Fetched(requested: 1, reviewed: 0, union: 1, derived: 1),
            new ExclusionCounts(0, 0, 0, 0),
            rateLimit: null,
            [Identity(owner, "repo", 1)]
        );

    private static OwnerPollWindow Window(string owner) => new(owner, StartedAt, Completed());

    private static DateTimeOffset Completed() => StartedAt.AddSeconds(3);

    private static PullRequestIdentity Identity(string owner, string repository, int number) =>
        new(
            id: $"{owner}/{repository}#{number}",
            owner: owner,
            repository: repository,
            number: number,
            title: "Add feature",
            url: $"https://github.com/{owner}/{repository}/pull/{number}",
            authorLogin: "author"
        );
}
