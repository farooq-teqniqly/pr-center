using Microsoft.Extensions.Logging;
using PrCenter.Core.Derivation;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Facts;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;

namespace PrCenter.Core.Queue;

/// <summary>
/// Use case that refreshes the review queue: it enumerates the owners with a
/// stored token, and for each owner resolves the authenticated login, fetches
/// that owner's review-queue facts, and derives the shown queue items -- deriving
/// each pull request's update baseline from its own facts (the user's latest
/// review instant), everything evaluated relative to the user. It then publishes
/// a new snapshot of the derived items and each owner's fetch status, with a pull
/// request more than one owner returned published once. A per-owner
/// fetch failure degrades only that owner; a locked vault mid-poll aborts the
/// whole refresh without touching the previously published snapshot.
/// </summary>
/// <remarks>
/// Every exit path -- success, an aborting vault lock, a shutdown cancellation,
/// and a fault in either the owner enumeration or the publish -- writes exactly
/// one diagnostics record to every registered sink, so the paths that produce no
/// snapshot are the ones a reader can still account for. Diagnostics are only
/// ever written here, never read: no membership, update, or covered decision
/// consults them.
/// </remarks>
public sealed partial class RefreshQueue : IRefreshQueue
{
    // Each sink write gets its own bounded budget rather than the caller's token,
    // which on the shutdown path is already canceled. Bounded rather than
    // unbounded because the host shutdown timeout is the only thing standing
    // between a blocked write and a killed container.
    private static readonly TimeSpan SinkWriteBudget = TimeSpan.FromSeconds(2);

    private readonly ITokenVault _vault;
    private readonly IGitHubFacts _facts;
    private readonly QueueSnapshotHolder _holder;
    private readonly ILogger<RefreshQueue> _logger;
    private readonly IEnumerable<IPollDiagnosticsSink> _sinks;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshQueue"/> class.
    /// </summary>
    /// <param name="vault">The vault enumerating the owners to poll.</param>
    /// <param name="facts">The GitHub facts port for login resolution and fetches.</param>
    /// <param name="holder">The holder the refreshed snapshot is published into.</param>
    /// <param name="logger">The logger for the aborted-poll and diagnostics-write warning paths.</param>
    /// <param name="sinks">The sinks each poll's diagnostics record is written to.</param>
    /// <param name="timeProvider">The clock stamping the poll and per-owner windows.</param>
    public RefreshQueue(
        ITokenVault vault,
        IGitHubFacts facts,
        QueueSnapshotHolder holder,
        ILogger<RefreshQueue> logger,
        IEnumerable<IPollDiagnosticsSink> sinks,
        TimeProvider timeProvider
    )
    {
        _vault = vault;
        _facts = facts;
        _holder = holder;
        _logger = logger;
        _sinks = sinks;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<RefreshOutcome> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var pollId = Guid.NewGuid();
        var startedAt = _timeProvider.GetUtcNow();

        // Null until the owner enumeration returns: a pass cannot name its
        // configured owners before they are known, and that path must still record
        // the poll that failed hardest.
        RefreshPass? pass = null;
        var outcome = PollOutcome.Faulted;
        int? publishedCount = null;

        try
        {
            // Inside the try, so a failure to enumerate is recorded rather than
            // escaping before the record exists.
            var owners = await _vault.ListOwnersAsync(cancellationToken).ConfigureAwait(false);

            // The last published snapshot is the source for carrying a failed owner's
            // rows over as stale; an owner no longer listed is simply not iterated, so
            // its rows drop out -- correct, it is no longer polled.
            var previous = _holder.Current;
            pass = new RefreshPass(new PollDiagnosticsAccumulator(pollId, startedAt, owners));

            foreach (var owner in owners)
            {
                await RefreshOwnerAsync(owner, previous, pass, cancellationToken)
                    .ConfigureAwait(false);
            }

            var snapshot = _holder.Publish(pass.Items.DistinctPullRequests(), pass.Statuses);
            publishedCount = snapshot.Items.Count;
            outcome = PollOutcome.Succeeded;
            return RefreshSucceeded.Instance;
        }
        // A locked vault is a global precondition failure, not a per-owner one:
        // abandon the whole refresh (no publish, so the last good snapshot
        // survives) and log it. The outcome is returned rather than thrown so the
        // poll loop can report a stale queue to the user without lock-specific
        // handling of its own.
        catch (VaultLockedException ex)
        {
            LogVaultLockedDuringRefresh(ex);
            outcome = PollOutcome.AbortedByLock;
            return RefreshAbortedByLock.Instance;
        }
        // A shutdown cancellation still propagates to stop the poll loop; it is
        // caught only long enough to record that the poll never finished, which is
        // not the same as having failed.
        catch (OperationCanceledException)
        {
            outcome = PollOutcome.Canceled;
            throw;
        }
        finally
        {
            // Anything else -- a fault in the enumeration or the publish -- leaves the
            // outcome at its initial Faulted and propagates unobserved.
            await WriteDiagnosticsAsync(pollId, startedAt, pass, outcome, publishedCount)
                .ConfigureAwait(false);
        }
    }

    private async Task WriteDiagnosticsAsync(
        Guid pollId,
        DateTimeOffset startedAt,
        RefreshPass? pass,
        PollOutcome outcome,
        int? publishedCount
    )
    {
        var completedAt = _timeProvider.GetUtcNow();

        // No pass means the enumeration never completed, so nothing is known about
        // which owners there were: absent configured owners and no rows, distinct
        // from a vault that legitimately holds none.
        PollDiagnostics record;
        if (pass is null)
        {
            record = new PollDiagnostics(
                new PollRunDiagnostics(pollId, startedAt, completedAt, outcome),
                []
            );
        }
        else
        {
            pass.Diagnostics.MarkRemainingUnreached();
            record = pass.Diagnostics.Build(completedAt, outcome, publishedCount);
        }

        foreach (var sink in _sinks)
        {
            // Per sink rather than one budget shared across the loop: the writes are
            // sequential, so a shared timer would let a slow first sink spend the
            // time the later sinks were promised and hand them an already-expired
            // token.
            using var writeBudget = new CancellationTokenSource(SinkWriteBudget, _timeProvider);

            try
            {
                await sink.WriteAsync(record, writeBudget.Token).ConfigureAwait(false);
            }
            // Per sink, and never rethrown: this runs in a finally, where a thrown
            // exception would replace the one already in flight and let a
            // diagnostics failure disguise a real cancellation or fault. One
            // failing sink also must not deny the others their write.
            catch (Exception ex)
            {
                LogDiagnosticsWriteFailed(sink.GetType().Name, ex);
            }
        }
    }

    private async Task RefreshOwnerAsync(
        string owner,
        QueueSnapshot? previous,
        RefreshPass pass,
        CancellationToken cancellationToken
    )
    {
        var startedAt = _timeProvider.GetUtcNow();
        try
        {
            // Resolved per owner per poll: a replaced PAT must not read as a stale
            // login, and the saving from caching across polls is negligible at a
            // multi-minute cadence.
            var myLogin = await _facts
                .GetAuthenticatedUserLoginAsync(owner, cancellationToken)
                .ConfigureAwait(false);
            var result = await _facts
                .GetReviewQueueFactsAsync(owner, myLogin, cancellationToken)
                .ConfigureAwait(false);

            if (result.Status is not OwnerFetchStatus.Ok)
            {
                CarryOverStaleOwner(
                    previous,
                    pass,
                    Window(owner, startedAt),
                    new OwnerPollOutcome(result.Status, result.Detail, myLogin)
                );
                return;
            }

            // Accumulate into a local list first so a fault part-way through cannot
            // leave a half-derived owner in the published snapshot.
            var ownerItems = new List<QueueItem>();
            var exclusions = new List<MembershipExclusion>();
            foreach (var facts in result.Facts)
            {
                // The update baseline is derived from each pull request's own
                // facts (my latest review instant); no stored marker is read.
                var derived = QueueItemDeriver.Derive(facts, myLogin);
                if (derived.Item is { } item)
                {
                    ownerItems.Add(item);
                }
                else if (derived.Exclusion is { } exclusion)
                {
                    exclusions.Add(exclusion);
                }
            }

            pass.Items.AddFresh(ownerItems);
            pass.Statuses.Add(new OwnerStatus(owner, OwnerFetchStatus.Ok));
            pass.Diagnostics.RecordPolled(
                Window(owner, startedAt),
                new OwnerPollOutcome(OwnerFetchStatus.Ok, resolvedLogin: myLogin),
                FetchCounts.Fetched(
                    result.Diagnostics?.RequestedCount,
                    result.Diagnostics?.ReviewedCount,
                    result.Facts.Count,
                    ownerItems.Count
                ),
                ExclusionCounts.Tally(exclusions),
                result.Diagnostics?.RateLimit,
                [.. ownerItems.Select(item => item.Identity)]
            );
        }
        // The vault crypto lock is a global abort -- rethrown to ExecuteAsync -- and a
        // real shutdown cancellation propagates to stop the loop. Any other fault
        // (a thrown login on auth/network/missing token, a timeout, or an
        // unexpected error) degrades only this owner: "a per-owner fetch failure
        // degrades only that owner." GetReviewQueueFactsAsync already reports its
        // own failures as a status; this guard covers the throwing members.
        catch (Exception ex)
            when (ex is not VaultLockedException
                && !(ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
            )
        {
            LogOwnerFetchFailed(owner, ex);
            // A friendly, transport-neutral detail rather than the raw exception
            // text: a timed-out request reads as a timeout, everything else as a
            // generic fetch failure. The full exception is in the log above.
            var detail =
                ex is OperationCanceledException
                    ? "The GitHub request timed out."
                    : "The owner's review queue could not be fetched.";
            CarryOverStaleOwner(
                previous,
                pass,
                Window(owner, startedAt),
                new OwnerPollOutcome(OwnerFetchStatus.Error, detail)
            );
        }
    }

    // A failed owner keeps the rows from the last snapshot rather than vanishing,
    // so a broken token does not silently empty that owner. Each carried status is
    // stamped with when the owner was last fresh; the fresh instant chains forward
    // across consecutive failures. An owner that has never been fresh (fails on its
    // first poll) carries no rows and a null instant.
    private static void CarryOverStaleOwner(
        QueueSnapshot? previous,
        RefreshPass pass,
        OwnerPollWindow window,
        OwnerPollOutcome outcome
    )
    {
        var owner = window.Owner;
        pass.Statuses.Add(
            new OwnerStatus(
                owner,
                outcome.Status,
                outcome.Detail,
                LastFreshInstant(previous, owner)
            )
        );

        QueueItem[] carried = previous is null
            ? []
            :
            [
                .. previous.Items.Where(item =>
                    string.Equals(item.Identity.Owner, owner, StringComparison.OrdinalIgnoreCase)
                ),
            ];

        pass.Items.CarryOver(carried);
        pass.Diagnostics.RecordCarriedOver(
            window,
            outcome,
            carried.Length,
            [.. carried.Select(item => item.Identity)]
        );
    }

    // The instant this owner's rows were last fresh: the previous snapshot's own
    // instant when the owner was Ok in it, otherwise the fresh instant already
    // carried on the previous (also failed) status -- so consecutive failures keep
    // pointing at the original fresh poll. Null when the owner was absent before.
    private static DateTimeOffset? LastFreshInstant(QueueSnapshot? previous, string owner)
    {
        if (previous is null)
        {
            return null;
        }

        var previousStatus = previous.OwnerStatuses.FirstOrDefault(status =>
            string.Equals(status.Owner, owner, StringComparison.OrdinalIgnoreCase)
        );

        if (previousStatus is null)
        {
            return null;
        }

        return previousStatus.Status is OwnerFetchStatus.Ok
            ? previous.SnapshotAt
            : previousStatus.LastFreshAt;
    }

    private OwnerPollWindow Window(string owner, DateTimeOffset startedAt) =>
        new(owner, startedAt, _timeProvider.GetUtcNow());
}
