using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IPollDiagnosticsReader"/> over the recorded
/// polls. Separate from <see cref="SqlitePollDiagnosticsSink"/> so that no type
/// on the refresh or derivation path can reach a read member at all.
/// </summary>
internal sealed partial class SqlitePollDiagnosticsReader : IPollDiagnosticsReader
{
    private readonly PrCenterDbContext _context;
    private readonly ILogger<SqlitePollDiagnosticsReader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlitePollDiagnosticsReader"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    /// <param name="logger">The logger for the dropped-poll warning path.</param>
    public SqlitePollDiagnosticsReader(
        PrCenterDbContext context,
        ILogger<SqlitePollDiagnosticsReader> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PollDiagnostics>> GetRecentPollsAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        if (count <= 0)
        {
            return [];
        }

        return await NewestReadablePollsAsync(count, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Walks the stored polls newest first and stops at the first
    /// <paramref name="count"/> readable ones, so an unreadable poll is backfilled
    /// from the polls behind it rather than shortening the result -- which is why
    /// the cap lives here and not in the query. The walk reads past
    /// <paramref name="count"/> rows only when a poll is dropped, and the ring
    /// bounds it either way.
    /// </summary>
    private async Task<IReadOnlyList<PollDiagnostics>> NewestReadablePollsAsync(
        int count,
        CancellationToken cancellationToken
    )
    {
        var polls = new List<PollDiagnostics>(count);

        await foreach (
            var stored in NewestPolls()
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            if (ReadPoll(stored) is { } poll)
            {
                polls.Add(poll);
            }

            if (polls.Count == count)
            {
                break;
            }
        }

        return polls;
    }

    // Newest first by key, which is also the insert order. Read-only, so nothing
    // belongs in the change tracker; the owner rows are ordered explicitly rather
    // than relying on the join's incidental order.
    private IQueryable<PollRun> NewestPolls() =>
        _context
            .PollRuns.AsNoTracking()
            .Include(run => run.Owners.OrderBy(owner => owner.Id))
            .OrderByDescending(run => run.Id);

    /// <summary>
    /// Reads one stored poll, or null when a stored value cannot be read back --
    /// only reachable by editing the file by hand, since the sink is its sole
    /// writer. That poll is dropped rather than invented, and rather than failing
    /// the read of the polls around it.
    /// </summary>
    private PollDiagnostics? ReadPoll(PollRun stored)
    {
        try
        {
            return new PollDiagnostics(ReadRun(stored), [.. stored.Owners.Select(ReadOwner)]);
        }
        catch (ArgumentException ex)
        {
            LogPollDropped(stored.PollId, ex);
            return null;
        }
    }

    private static PollRunDiagnostics ReadRun(PollRun stored) =>
        new(
            stored.PollId,
            stored.StartedAt,
            stored.CompletedAt,
            Enum.Parse<PollOutcome>(stored.Outcome),
            stored.ConfiguredOwners,
            stored.PublishedCount
        );

    private static OwnerPollDiagnostics ReadOwner(PollOwnerDiagnostic stored) =>
        new(
            new OwnerPollWindow(stored.Owner, stored.StartedAt, stored.CompletedAt),
            new OwnerPollOutcome(
                Enum.Parse<OwnerFetchStatus>(stored.Status),
                stored.Detail,
                stored.ResolvedLogin
            ),
            ReadCounts(stored),
            ReadExclusions(stored),
            ReadRateLimit(stored),
            new ContributedPullRequests(stored.PullRequestIds, stored.ForeignItemCount)
        );

    // The carry-over count is the discriminator: the sink writes it for every row
    // it reached (zero on a successful fetch) and leaves it null only for an owner
    // the poll never completed.
    private static FetchCounts? ReadCounts(PollOwnerDiagnostic stored) =>
        stored.CarriedOverCount is { } carriedOver
            ? new FetchCounts(
                stored.RequestedCount,
                stored.ReviewedCount,
                stored.UnionCount,
                stored.DerivedCount,
                carriedOver
            )
            : null;

    // The four tallies are written together, so one present means all are.
    private static ExclusionCounts? ReadExclusions(PollOwnerDiagnostic stored) =>
        stored.DraftExclusions is { } draft
            ? new ExclusionCounts(
                draft,
                stored.ClosedOrMergedExclusions ?? 0,
                stored.ApprovedExclusions ?? 0,
                stored.UntrackedExclusions ?? 0
            )
            : null;

    private static RateLimitReading? ReadRateLimit(PollOwnerDiagnostic stored) =>
        stored
            is {
                RateLimitRemaining: { } remaining,
                RateLimitResetAt: { } resetAt,
                RateLimitCost: { } cost
            }
            ? new RateLimitReading(remaining, resetAt, cost)
            : null;
}
