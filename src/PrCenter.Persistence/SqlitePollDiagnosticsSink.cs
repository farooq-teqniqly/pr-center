using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IPollDiagnosticsSink"/> over the local SQLite
/// file, as a bounded ring of the most recent polls. Write-only by design: the
/// read surface is <see cref="SqlitePollDiagnosticsReader"/>, so nothing on the
/// refresh path can reach a read member.
/// </summary>
internal sealed partial class SqlitePollDiagnosticsSink : IPollDiagnosticsSink
{
    /// <summary>
    /// How many polls the ring keeps. At the five-minute default interval this is
    /// roughly sixteen hours -- long enough to cover "it looked wrong when I got in
    /// this morning" -- and at a handful of owners each it is kilobytes, not
    /// megabytes.
    /// </summary>
    internal const int RetainedPolls = 200;

    private readonly PrCenterDbContext _context;
    private readonly ILogger<SqlitePollDiagnosticsSink> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlitePollDiagnosticsSink"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    /// <param name="logger">The logger for the eviction record.</param>
    public SqlitePollDiagnosticsSink(
        PrCenterDbContext context,
        ILogger<SqlitePollDiagnosticsSink> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="diagnostics"/> is null.</exception>
    public async Task WriteAsync(
        PollDiagnostics diagnostics,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        // Insert and trim in one transaction, so a failure part-way through leaves
        // neither a new poll nor a half-applied eviction. A reader must never see a
        // ring that lost a poll without gaining one.
        await using var transaction = await _context
            .Database.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        _context.PollRuns.Add(ToEntity(diagnostics));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await TrimAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    // Trims by poll rather than by row count, so a five-owner poll does not evict
    // polls unevenly. The owner rows go with the parent through the cascade, which
    // is why they are not deleted here: a second pass could leave a partial poll.
    private async Task TrimAsync(CancellationToken cancellationToken)
    {
        var surviving = _context
            .PollRuns.OrderByDescending(run => run.Id)
            .Select(run => run.Id)
            .Take(RetainedPolls);

        var evicted = await _context
            .PollRuns.Where(run => !surviving.Contains(run.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (evicted > 0)
        {
            LogPollsEvicted(evicted, RetainedPolls);
        }
    }

    private static PollRun ToEntity(PollDiagnostics diagnostics) =>
        new()
        {
            PollId = diagnostics.Run.PollId,
            StartedAt = diagnostics.Run.StartedAt,
            CompletedAt = diagnostics.Run.CompletedAt,
            Outcome = diagnostics.Run.Outcome.ToString(),
            ConfiguredOwners = diagnostics.Run.ConfiguredOwners,

            // Derived from the list rather than counted from the owner rows, so the
            // column and the list can never disagree, and absent stays absent.
            OwnerCount = diagnostics.Run.ConfiguredOwners?.Count,
            PublishedCount = diagnostics.Run.PublishedCount,
            Owners = [.. diagnostics.Owners.Select(ToEntity)],
        };

    private static PollOwnerDiagnostic ToEntity(OwnerPollDiagnostics owner) =>
        new()
        {
            Owner = owner.Window.Owner,
            StartedAt = owner.Window.StartedAt,
            CompletedAt = owner.Window.CompletedAt,
            Status = owner.Outcome.Status.ToString(),
            Detail = owner.Outcome.Detail,
            ResolvedLogin = owner.Outcome.ResolvedLogin,
            RequestedCount = owner.Counts?.Requested,
            ReviewedCount = owner.Counts?.Reviewed,
            UnionCount = owner.Counts?.Union,
            DerivedCount = owner.Counts?.Derived,
            CarriedOverCount = owner.Counts?.CarriedOver,
            DraftExclusions = owner.Exclusions?.Draft,
            ClosedOrMergedExclusions = owner.Exclusions?.ClosedOrMerged,
            ApprovedExclusions = owner.Exclusions?.Approved,
            UntrackedExclusions = owner.Exclusions?.Untracked,
            RateLimitRemaining = owner.RateLimit?.Remaining,
            RateLimitResetAt = owner.RateLimit?.ResetAt,
            RateLimitCost = owner.RateLimit?.Cost,
            PullRequestIds = owner.Contributed.Ids,
            ForeignItemCount = owner.Contributed.ForeignCount,
        };
}
