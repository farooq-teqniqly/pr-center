using Microsoft.EntityFrameworkCore;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence.Tests;

public sealed class SqlitePollDiagnosticsSinkTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task WriteAsync_WithNullDiagnostics_Throws()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var sink = CreateSink(context);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sink.WriteAsync(null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task WriteAsync_PersistsThePollLevelFields()
    {
        // Arrange
        var pollId = Guid.NewGuid();
        var record = PollDiagnosticsFactory.Full(pollId);

        // Act
        await WriteAsync(record);

        // Assert
        await using var read = _database.CreateContext();
        var stored = await read.PollRuns.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(pollId, stored.PollId);
        Assert.Equal(PollDiagnosticsFactory.StartedAt, stored.StartedAt);
        Assert.Equal(PollDiagnosticsFactory.StartedAt.AddSeconds(3), stored.CompletedAt);
        Assert.Equal(nameof(PollOutcome.Succeeded), stored.Outcome);
        Assert.Equal(5, stored.PublishedCount);
        Assert.Equal(2, stored.OwnerCount);
    }

    [Fact]
    public async Task WriteAsync_PersistsEveryFieldOfAPolledOwnerRow()
    {
        // Act
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Assert
        var stored = await ReadOwnerAsync("acme");
        Assert.Equal(nameof(OwnerFetchStatus.Ok), stored.Status);
        Assert.Equal("octocat", stored.ResolvedLogin);
        Assert.Equal(PollDiagnosticsFactory.StartedAt, stored.StartedAt);
        Assert.Equal(12, stored.RequestedCount);
        Assert.Equal(8, stored.ReviewedCount);
        Assert.Equal(15, stored.UnionCount);
        Assert.Equal(4, stored.DerivedCount);
        Assert.Equal(0, stored.CarriedOverCount);
        Assert.Equal(6, stored.DraftExclusions);
        Assert.Equal(2, stored.ClosedOrMergedExclusions);
        Assert.Equal(3, stored.ApprovedExclusions);
        Assert.Equal(0, stored.UntrackedExclusions);
        Assert.Equal(4987, stored.RateLimitRemaining);
        Assert.Equal(PollDiagnosticsFactory.StartedAt.AddHours(1), stored.RateLimitResetAt);
        Assert.Equal(13, stored.RateLimitCost);
    }

    [Fact]
    public async Task WriteAsync_PersistsContributedIdentifiersAndTheirForeignCount()
    {
        // Act
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Assert
        var stored = await ReadOwnerAsync("acme");
        Assert.Equal(["acme/api#12", "ps-unite/tools#3"], stored.PullRequestIds);
        Assert.Equal(1, stored.ForeignItemCount);
    }

    [Fact]
    public async Task WriteAsync_RoundTripsConfiguredOwnersThroughTheConverter()
    {
        // Act
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Assert
        await using var read = _database.CreateContext();
        var stored = await read.PollRuns.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(["acme", "ps-unite"], stored.ConfiguredOwners);
    }

    [Fact]
    public async Task WriteAsync_WhenAnOwnerWasNeverReached_PersistsNullInstantsAndNullCounts()
    {
        // Act
        await WriteAsync(PollDiagnosticsFactory.WithUnreachedOwner());

        // Assert
        var stored = await ReadOwnerAsync("acme");
        Assert.Equal(nameof(OwnerFetchStatus.NotPolled), stored.Status);
        Assert.Null(stored.StartedAt);
        Assert.Null(stored.CompletedAt);
        Assert.Null(stored.RequestedCount);
        Assert.Null(stored.UnionCount);
        Assert.Null(stored.CarriedOverCount);
        Assert.Null(stored.DraftExclusions);
        Assert.Null(stored.RateLimitRemaining);
        Assert.Empty(stored.PullRequestIds);
    }

    [Fact]
    public async Task WriteAsync_WhenAnOwnerCarriedNothingOver_KeepsZeroDistinctFromNull()
    {
        // Act
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Assert -- the failed owner carried zero rows, which is a claim; its fetch
        // counts are absent, which is a different one
        var stored = await ReadOwnerAsync("ps-unite");
        Assert.Equal(0, stored.CarriedOverCount);
        Assert.Null(stored.RequestedCount);
        Assert.Null(stored.UnionCount);
        Assert.Null(stored.DraftExclusions);
    }

    [Fact]
    public async Task WriteAsync_WithNoOwnerRows_PersistsThePollWithAbsentOwners()
    {
        // Act
        await WriteAsync(PollDiagnosticsFactory.WithoutOwners());

        // Assert
        await using var read = _database.CreateContext();
        var stored = await read.PollRuns.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(nameof(PollOutcome.Faulted), stored.Outcome);
        Assert.Null(stored.ConfiguredOwners);
        Assert.Null(stored.OwnerCount);
        Assert.Null(stored.PublishedCount);
        Assert.Empty(
            await read.PollOwnerDiagnostics.AsNoTracking().ToListAsync(CancellationToken.None)
        );
    }

    [Fact]
    public async Task WriteAsync_WithNoConfiguredOwners_KeepsTheEmptyListDistinctFromAbsent()
    {
        // Arrange -- a vault that legitimately holds no owners
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

        // Act
        await WriteAsync(record);

        // Assert
        await using var read = _database.CreateContext();
        var stored = await read.PollRuns.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.NotNull(stored.ConfiguredOwners);
        Assert.Empty(stored.ConfiguredOwners);
        Assert.Equal(0, stored.OwnerCount);
    }

    [Fact]
    public async Task WriteAsync_WhenTheRingIsFull_EvictsExactlyTheOldestPollAndItsOwnerRows()
    {
        // Arrange
        var oldest = (await FillRingAsync())[0];

        // Act
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Assert
        await using var read = _database.CreateContext();
        Assert.Equal(
            SqlitePollDiagnosticsSink.RetainedPolls,
            await read.PollRuns.CountAsync(CancellationToken.None)
        );
        Assert.False(
            await read.PollRuns.AnyAsync(run => run.PollId == oldest, CancellationToken.None)
        );
        Assert.False(
            await read.PollOwnerDiagnostics.AnyAsync(
                owner => owner.Owner == "evicted-owner",
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task WriteAsync_WhenTheStoreIsFarAboveTheRing_KeepsExactlyTheNewestRetainedPolls()
    {
        // Arrange -- fifty polls past the limit, so the trim is a bulk delete
        // against real SQLite rather than the single-row eviction of the steady state
        var seeded = await FillRingAsync(polls: SqlitePollDiagnosticsSink.RetainedPolls + 50);
        var newest = Guid.NewGuid();

        // Act
        await WriteAsync(PollDiagnosticsFactory.Full(newest));

        // Assert
        await using var read = _database.CreateContext();
        var surviving = await read
            .PollRuns.AsNoTracking()
            .OrderBy(run => run.Id)
            .Select(run => run.PollId)
            .ToListAsync(CancellationToken.None);
        Assert.Equal([.. seeded.Skip(51), newest], surviving);
    }

    [Fact]
    public async Task WriteAsync_WhenTheRingIsFull_EvictsByPollRatherThanByOwnerRowCount()
    {
        // Arrange -- the oldest poll carries five owner rows, the rest one each
        await FillRingAsync(oldestOwnerRows: 5);

        // Act
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Assert -- one poll left, not five polls' worth of rows
        await using var read = _database.CreateContext();
        Assert.Equal(
            SqlitePollDiagnosticsSink.RetainedPolls,
            await read.PollRuns.CountAsync(CancellationToken.None)
        );
    }

    [Fact]
    public async Task WriteAsync_WhenTheWriteFails_LeavesNeitherTheNewPollNorAnEviction()
    {
        // Arrange -- a duplicate poll id violates the unique index mid-write
        var oldest = (await FillRingAsync())[0];
        var duplicated = await FirstStoredPollIdAsync();

        // Act
        await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
            WriteAsync(PollDiagnosticsFactory.Full(duplicated))
        );

        // Assert -- the store is exactly as it was: nothing added, nothing evicted
        await using var read = _database.CreateContext();
        Assert.Equal(
            SqlitePollDiagnosticsSink.RetainedPolls,
            await read.PollRuns.CountAsync(CancellationToken.None)
        );
        Assert.True(
            await read.PollRuns.AnyAsync(run => run.PollId == oldest, CancellationToken.None)
        );
    }

    [Fact]
    public async Task WriteAsync_BelowTheRetentionLimit_EvictsNothing()
    {
        // Act
        await WriteAsync(PollDiagnosticsFactory.Full());
        await WriteAsync(PollDiagnosticsFactory.Full());

        // Assert
        await using var read = _database.CreateContext();
        Assert.Equal(2, await read.PollRuns.CountAsync(CancellationToken.None));
    }

    // Seeds the store to the retention limit (or beyond), bypassing the sink so the
    // setup is one insert rather than N transactions. Returns the seeded poll ids,
    // oldest first.
    private async Task<IReadOnlyList<Guid>> FillRingAsync(
        int oldestOwnerRows = 1,
        int polls = SqlitePollDiagnosticsSink.RetainedPolls
    )
    {
        await using var context = _database.CreateContext();
        var seeded = new List<Guid>(polls);

        for (var index = 0; index < polls; index++)
        {
            var isOldest = index == 0;
            var owners = isOldest ? oldestOwnerRows : 1;
            var pollId = Guid.NewGuid();
            seeded.Add(pollId);
            context.PollRuns.Add(
                new PollRun
                {
                    PollId = pollId,
                    StartedAt = PollDiagnosticsFactory.StartedAt.AddMinutes(index),
                    CompletedAt = PollDiagnosticsFactory.StartedAt.AddMinutes(index).AddSeconds(2),
                    Outcome = nameof(PollOutcome.Succeeded),
                    ConfiguredOwners = ["seeded"],
                    OwnerCount = owners,
                    PublishedCount = 1,
                    Owners =
                    [
                        .. Enumerable
                            .Range(0, owners)
                            .Select(row => new PollOwnerDiagnostic
                            {
                                Owner = isOldest ? "evicted-owner" : $"seeded-{row}",
                                Status = nameof(OwnerFetchStatus.Ok),
                                PullRequestIds = [],
                            }),
                    ],
                }
            );
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return seeded;
    }

    private async Task<Guid> FirstStoredPollIdAsync()
    {
        await using var context = _database.CreateContext();
        return await context
            .PollRuns.AsNoTracking()
            .OrderBy(run => run.Id)
            .Select(run => run.PollId)
            .FirstAsync(CancellationToken.None);
    }

    private async Task WriteAsync(PollDiagnostics record)
    {
        await using var context = _database.CreateContext();
        await CreateSink(context).WriteAsync(record, CancellationToken.None);
    }

    private async Task<PollOwnerDiagnostic> ReadOwnerAsync(string owner)
    {
        await using var context = _database.CreateContext();
        return await context
            .PollOwnerDiagnostics.AsNoTracking()
            .SingleAsync(row => row.Owner == owner, CancellationToken.None);
    }

    private static SqlitePollDiagnosticsSink CreateSink(PrCenterDbContext context) =>
        new(context, new CapturingLogger<SqlitePollDiagnosticsSink>());

    public void Dispose() => _database.Dispose();
}
