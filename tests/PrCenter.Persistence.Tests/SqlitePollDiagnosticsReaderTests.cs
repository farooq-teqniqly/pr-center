using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence.Tests;

public sealed class SqlitePollDiagnosticsReaderTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();
    private readonly CapturingLogger<SqlitePollDiagnosticsReader> _logger = new();

    [Fact]
    public async Task GetRecentPollsAsync_WithAnEmptyStore_ReturnsNoPolls()
    {
        // Act
        var polls = await ReadAsync(10);

        // Assert
        Assert.Empty(polls);
    }

    [Fact]
    public async Task GetRecentPollsAsync_ReturnsTheNewestPollsFirst()
    {
        // Arrange
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        await WriteAsync(PollDiagnosticsFactory.Full(first));
        await WriteAsync(PollDiagnosticsFactory.Full(second));
        await WriteAsync(PollDiagnosticsFactory.Full(third));

        // Act
        var polls = await ReadAsync(10);

        // Assert
        Assert.Equal([third, second, first], polls.Select(poll => poll.Run.PollId));
    }

    [Fact]
    public async Task GetRecentPollsAsync_CapsTheResultAtTheRequestedCount()
    {
        // Arrange
        var newest = Guid.NewGuid();
        await WriteAsync(PollDiagnosticsFactory.Full());
        await WriteAsync(PollDiagnosticsFactory.Full());
        await WriteAsync(PollDiagnosticsFactory.Full(newest));

        // Act
        var polls = await ReadAsync(2);

        // Assert
        Assert.Equal(2, polls.Count);
        Assert.Equal(newest, polls[0].Run.PollId);
    }

    [Fact]
    public async Task GetRecentPollsAsync_ReturnsThePollLevelFields()
    {
        // Arrange
        var pollId = Guid.NewGuid();
        await WriteAsync(PollDiagnosticsFactory.Full(pollId));

        // Act
        var poll = Assert.Single(await ReadAsync(10));

        // Assert
        Assert.Equal(pollId, poll.Run.PollId);
        Assert.Equal(PollDiagnosticsFactory.StartedAt, poll.Run.StartedAt);
        Assert.Equal(PollDiagnosticsFactory.StartedAt.AddSeconds(3), poll.Run.CompletedAt);
        Assert.Equal(PollOutcome.Succeeded, poll.Run.Outcome);
        Assert.Equal(["acme", "ps-unite"], poll.Run.ConfiguredOwners);
        Assert.Equal(5, poll.Run.PublishedCount);
    }

    [Fact]
    public async Task GetRecentPollsAsync_ReturnsEachPollsOwnerRows()
    {
        // Arrange
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Act
        var poll = Assert.Single(await ReadAsync(10));

        // Assert
        Assert.Equal(["acme", "ps-unite"], poll.Owners.Select(row => row.Window.Owner));
    }

    [Fact]
    public async Task GetRecentPollsAsync_ReturnsAPolledOwnersCountsAndRateLimit()
    {
        // Arrange
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Act
        var row = Row(Assert.Single(await ReadAsync(10)), "acme");

        // Assert
        Assert.Equal(OwnerFetchStatus.Ok, row.Outcome.Status);
        Assert.Equal("octocat", row.Outcome.ResolvedLogin);
        Assert.Equal(new FetchCounts(12, 8, 15, 4, 0), row.Counts);
        Assert.Equal(new ExclusionCounts(6, 2, 3, 0), row.Exclusions);
        Assert.Equal(
            new RateLimitReading(4987, PollDiagnosticsFactory.StartedAt.AddHours(1), 13),
            row.RateLimit
        );
    }

    [Fact]
    public async Task GetRecentPollsAsync_ReturnsAnOwnersContributedIdentifiersAndForeignCount()
    {
        // Arrange
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Act
        var row = Row(Assert.Single(await ReadAsync(10)), "acme");

        // Assert
        Assert.Equal(["acme/api#12", "ps-unite/tools#3"], row.Contributed.Ids);
        Assert.Equal(1, row.Contributed.ForeignCount);
    }

    [Fact]
    public async Task GetRecentPollsAsync_WhenAnOwnerCarriedOver_ReturnsAbsentFetchCountsAndExclusions()
    {
        // Arrange
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Act
        var row = Row(Assert.Single(await ReadAsync(10)), "ps-unite");

        // Assert -- the carry-over count survives while the fetch numbers read as absent
        Assert.NotNull(row.Counts);
        Assert.Equal(0, row.Counts.CarriedOver);
        Assert.Null(row.Counts.Union);
        Assert.Null(row.Exclusions);
        Assert.Null(row.RateLimit);
    }

    [Fact]
    public async Task GetRecentPollsAsync_WhenAnOwnerWasNeverReached_ReturnsNotPolledWithNoCounts()
    {
        // Arrange
        await WriteAsync(PollDiagnosticsFactory.WithUnreachedOwner());

        // Act
        var row = Assert.Single(Assert.Single(await ReadAsync(10)).Owners);

        // Assert
        Assert.Equal(OwnerFetchStatus.NotPolled, row.Outcome.Status);
        Assert.Null(row.Window.StartedAt);
        Assert.Null(row.Counts);
        Assert.Null(row.Exclusions);
        Assert.Empty(row.Contributed.Ids);
    }

    [Fact]
    public async Task GetRecentPollsAsync_WhenTheOwnerEnumerationFailed_ReturnsAbsentConfiguredOwners()
    {
        // Arrange
        await WriteAsync(PollDiagnosticsFactory.WithoutOwners());

        // Act
        var poll = Assert.Single(await ReadAsync(10));

        // Assert -- absent, so a broken vault does not read as an empty one
        Assert.Null(poll.Run.ConfiguredOwners);
        Assert.Empty(poll.Owners);
        Assert.Null(poll.Run.PublishedCount);
    }

    [Fact]
    public async Task GetRecentPollsAsync_WhenNoOwnersAreConfigured_ReturnsAnEmptyOwnerList()
    {
        // Arrange
        var record = new PollDiagnostics(
            new PollRunDiagnostics(
                Guid.NewGuid(),
                PollDiagnosticsFactory.StartedAt,
                PollDiagnosticsFactory.StartedAt.AddSeconds(1),
                PollOutcome.Succeeded,
                configuredOwners: [],
                publishedCount: 0
            ),
            []
        );
        await WriteAsync(record);

        // Act
        var poll = Assert.Single(await ReadAsync(10));

        // Assert
        Assert.NotNull(poll.Run.ConfiguredOwners);
        Assert.Empty(poll.Run.ConfiguredOwners);
    }

    [Theory]
    [InlineData(PollOutcome.Succeeded)]
    [InlineData(PollOutcome.AbortedByLock)]
    [InlineData(PollOutcome.Canceled)]
    [InlineData(PollOutcome.Faulted)]
    public async Task GetRecentPollsAsync_RoundTripsEveryOutcome(PollOutcome outcome)
    {
        // Arrange
        await WriteAsync(PollDiagnosticsFactory.WithoutOwners(outcome: outcome));

        // Act
        var poll = Assert.Single(await ReadAsync(10));

        // Assert
        Assert.Equal(outcome, poll.Run.Outcome);
    }

    [Theory]
    [InlineData("poll outcome")]
    [InlineData("owner status")]
    public async Task GetRecentPollsAsync_WhenAStoredEnumIsUnreadable_DropsOnlyThatPoll(
        string column
    )
    {
        // Arrange
        var corrupted = Guid.NewGuid();
        var intact = Guid.NewGuid();
        await WriteAsync(PollDiagnosticsFactory.Full(corrupted));
        await WriteAsync(PollDiagnosticsFactory.Full(intact));
        await CorruptAsync(corrupted, column);

        // Act
        var polls = await ReadAsync(10);

        // Assert
        Assert.Equal(intact, Assert.Single(polls).Run.PollId);
    }

    [Fact]
    public async Task GetRecentPollsAsync_WhenADroppedPollIsInsideTheRequestedWindow_BacksFillFromOlderPolls()
    {
        // Arrange
        var oldest = Guid.NewGuid();
        var corrupted = Guid.NewGuid();
        var newest = Guid.NewGuid();
        await WriteAsync(PollDiagnosticsFactory.Full(oldest));
        await WriteAsync(PollDiagnosticsFactory.Full(corrupted));
        await WriteAsync(PollDiagnosticsFactory.Full(newest));
        await CorruptAsync(corrupted, "poll outcome");

        // Act
        var polls = await ReadAsync(2);

        // Assert
        Assert.Equal([newest, oldest], polls.Select(poll => poll.Run.PollId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRecentPollsAsync_WithANonPositiveCount_ReturnsNoPolls(int count)
    {
        // Arrange
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Act
        var polls = await ReadAsync(count);

        // Assert
        Assert.Empty(polls);
    }

    [Theory]
    [InlineData("closed or merged")]
    [InlineData("approved")]
    [InlineData("untracked")]
    public async Task GetRecentPollsAsync_WhenOnlySomeExclusionTalliesAreStored_ReturnsAbsentExclusions(
        string tally
    )
    {
        // Arrange
        var pollId = Guid.NewGuid();
        await WriteAsync(PollDiagnosticsFactory.Full(pollId));
        await ClearExclusionAsync(pollId, tally);

        // Act
        var row = Row(Assert.Single(await ReadAsync(10)), "acme");

        // Assert -- absent, so a half-written row does not read as zero exclusions
        Assert.Null(row.Exclusions);
    }

    [Fact]
    public async Task GetRecentPollsAsync_WhenAStoredEnumIsUnreadable_WarnsWithTheDroppedPollsId()
    {
        // Arrange
        var corrupted = Guid.NewGuid();
        await WriteAsync(PollDiagnosticsFactory.Full(corrupted));
        await CorruptAsync(corrupted, "poll outcome");

        // Act
        await ReadAsync(10);

        // Assert
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(corrupted.ToString(), entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static OwnerPollDiagnostics Row(PollDiagnostics poll, string owner) =>
        Assert.Single(poll.Owners, row => row.Window.Owner == owner);

    private async Task<IReadOnlyList<PollDiagnostics>> ReadAsync(int count)
    {
        await using var context = _database.CreateContext();
        return await new SqlitePollDiagnosticsReader(context, _logger).GetRecentPollsAsync(
            count,
            CancellationToken.None
        );
    }

    // Writes a value into a stored enum column that no version of the sink could
    // have written, which is what a hand-edited file looks like to the reader.
    private async Task CorruptAsync(Guid pollId, string column)
    {
        await using var context = _database.CreateContext();

        if (column == "poll outcome")
        {
            await context
                .PollRuns.Where(run => run.PollId == pollId)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(run => run.Outcome, "nonsense"),
                    CancellationToken.None
                );
            return;
        }

        await context
            .PollOwnerDiagnostics.Where(owner => owner.PollRun.PollId == pollId)
            .ExecuteUpdateAsync(
                update => update.SetProperty(owner => owner.Status, "nonsense"),
                CancellationToken.None
            );
    }

    // Nulls out one exclusion tally while the others stay written, which is what
    // a hand-edited file looks like to the reader -- the sink writes all four
    // together or none.
    private async Task ClearExclusionAsync(Guid pollId, string tally)
    {
        await using var context = _database.CreateContext();
        var owners = context.PollOwnerDiagnostics.Where(owner =>
            owner.PollRun.PollId == pollId && owner.Owner == "acme"
        );

        await (
            tally switch
            {
                "closed or merged" => owners.ExecuteUpdateAsync(
                    update =>
                        update.SetProperty(owner => owner.ClosedOrMergedExclusions, (int?)null),
                    CancellationToken.None
                ),
                "approved" => owners.ExecuteUpdateAsync(
                    update => update.SetProperty(owner => owner.ApprovedExclusions, (int?)null),
                    CancellationToken.None
                ),
                _ => owners.ExecuteUpdateAsync(
                    update => update.SetProperty(owner => owner.UntrackedExclusions, (int?)null),
                    CancellationToken.None
                ),
            }
        );
    }

    private async Task WriteAsync(PollDiagnostics record)
    {
        await using var context = _database.CreateContext();
        await new SqlitePollDiagnosticsSink(
            context,
            new CapturingLogger<SqlitePollDiagnosticsSink>()
        ).WriteAsync(record, CancellationToken.None);
    }

    public void Dispose() => _database.Dispose();
}
