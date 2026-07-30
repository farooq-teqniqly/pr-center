using Microsoft.EntityFrameworkCore;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IPollDiagnosticsReader"/> over the recorded
/// polls. Separate from <see cref="SqlitePollDiagnosticsSink"/> so that no type
/// on the refresh or derivation path can reach a read member at all.
/// </summary>
internal sealed class SqlitePollDiagnosticsReader : IPollDiagnosticsReader
{
    private readonly PrCenterDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlitePollDiagnosticsReader"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    public SqlitePollDiagnosticsReader(PrCenterDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PollDiagnostics>> GetRecentPollsAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        // Projected rather than materialized as entities: nothing here is mutated
        // and saved back, so no row belongs in the change tracker. Newest first by
        // key, which is also the insert order.
        var rows = await _context
            .PollRuns.AsNoTracking()
            .OrderByDescending(run => run.Id)
            .Take(count)
            .Select(run => new
            {
                run.PollId,
                run.StartedAt,
                run.CompletedAt,
                run.Outcome,
                run.ConfiguredOwners,
                run.PublishedCount,
                Owners = run
                    .Owners.Select(owner => new
                    {
                        owner.Owner,
                        owner.StartedAt,
                        owner.CompletedAt,
                        owner.Status,
                        owner.Detail,
                        owner.ResolvedLogin,
                        owner.RequestedCount,
                        owner.ReviewedCount,
                        owner.UnionCount,
                        owner.DerivedCount,
                        owner.CarriedOverCount,
                        owner.DraftExclusions,
                        owner.ClosedOrMergedExclusions,
                        owner.ApprovedExclusions,
                        owner.UntrackedExclusions,
                        owner.RateLimitRemaining,
                        owner.RateLimitResetAt,
                        owner.RateLimitCost,
                        owner.PullRequestIds,
                        owner.ForeignItemCount,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new PollDiagnostics(
                new PollRunDiagnostics(
                    row.PollId,
                    row.StartedAt,
                    row.CompletedAt,
                    // Parsed rather than defaulted: the only writer of this column
                    // is our own sink, so an unparseable value means the file was
                    // edited by hand, and inventing an outcome would put a fiction
                    // in front of the person debugging.
                    Enum.Parse<PollOutcome>(row.Outcome),
                    row.ConfiguredOwners,
                    row.PublishedCount
                ),
                [
                    .. row.Owners.Select(owner => new OwnerPollDiagnostics(
                        new OwnerPollWindow(owner.Owner, owner.StartedAt, owner.CompletedAt),
                        new OwnerPollOutcome(
                            Enum.Parse<OwnerFetchStatus>(owner.Status),
                            owner.Detail,
                            owner.ResolvedLogin
                        ),
                        // The carry-over count is the discriminator: the sink writes
                        // it for every row it reached (zero on a successful fetch)
                        // and leaves it null only for an owner never reached.
                        owner.CarriedOverCount
                            is { } carriedOver
                            ? new FetchCounts(
                                owner.RequestedCount,
                                owner.ReviewedCount,
                                owner.UnionCount,
                                owner.DerivedCount,
                                carriedOver
                            )
                            : null,
                        // The four exclusion tallies are written together, so one
                        // present means all are.
                        owner.DraftExclusions
                            is { } draft
                            ? new ExclusionCounts(
                                draft,
                                owner.ClosedOrMergedExclusions ?? 0,
                                owner.ApprovedExclusions ?? 0,
                                owner.UntrackedExclusions ?? 0
                            )
                            : null,
                        owner.RateLimitRemaining is { } remaining
                        && owner.RateLimitResetAt is { } resetAt
                        && owner.RateLimitCost is { } cost
                            ? new RateLimitReading(remaining, resetAt, cost)
                            : null,
                        new ContributedPullRequests(owner.PullRequestIds, owner.ForeignItemCount)
                    )),
                ]
            )),
        ];
    }
}
