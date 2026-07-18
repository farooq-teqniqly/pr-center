using Microsoft.Extensions.Logging;
using PrCenter.Core.Derivation;
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
/// a new snapshot of the derived items and each owner's fetch status. A per-owner
/// fetch failure degrades only that owner; a locked vault mid-poll aborts the
/// whole refresh without touching the previously published snapshot.
/// </summary>
public sealed partial class RefreshQueue : IRefreshQueue
{
    private readonly ITokenVault _vault;
    private readonly IGitHubFacts _facts;
    private readonly QueueSnapshotHolder _holder;
    private readonly ILogger<RefreshQueue> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshQueue"/> class.
    /// </summary>
    /// <param name="vault">The vault enumerating the owners to poll.</param>
    /// <param name="facts">The GitHub facts port for login resolution and fetches.</param>
    /// <param name="holder">The holder the refreshed snapshot is published into.</param>
    /// <param name="logger">The logger for the aborted-poll warning path.</param>
    public RefreshQueue(
        ITokenVault vault,
        IGitHubFacts facts,
        QueueSnapshotHolder holder,
        ILogger<RefreshQueue> logger
    )
    {
        _vault = vault;
        _facts = facts;
        _holder = holder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var owners = await _vault.ListOwnersAsync(cancellationToken).ConfigureAwait(false);

        // The last published snapshot is the source for carrying a failed owner's
        // rows over as stale; an owner no longer listed is simply not iterated, so
        // its rows drop out -- correct, it is no longer polled.
        var previous = _holder.Current;
        var items = new List<QueueItem>();
        var statuses = new List<OwnerStatus>();
        try
        {
            foreach (var owner in owners)
            {
                await RefreshOwnerAsync(owner, previous, items, statuses, cancellationToken)
                    .ConfigureAwait(false);
            }

            _holder.Publish(items, statuses);
        }
        // A locked vault is a global precondition failure, not a per-owner one:
        // abandon the whole refresh (no publish, so the last good snapshot
        // survives) and log it -- the one owner of the mid-poll-lock warning, so
        // the poll loop that calls this needs no lock-specific handling of its own.
        catch (VaultLockedException ex)
        {
            LogVaultLockedDuringRefresh(ex);
        }
    }

    private async Task RefreshOwnerAsync(
        string owner,
        QueueSnapshot? previous,
        List<QueueItem> items,
        List<OwnerStatus> statuses,
        CancellationToken cancellationToken
    )
    {
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
                CarryOverStaleOwner(owner, previous, items, statuses, result.Status, result.Detail);
                return;
            }

            // Accumulate into a local list first so a fault part-way through cannot
            // leave a half-derived owner in the published snapshot.
            var ownerItems = new List<QueueItem>();
            foreach (var facts in result.Facts)
            {
                // The update baseline is derived from each pull request's own
                // facts (my latest review instant); no stored marker is read.
                var item = QueueItemDeriver.Derive(facts, myLogin);
                if (item is not null)
                {
                    ownerItems.Add(item);
                }
            }

            items.AddRange(ownerItems);
            statuses.Add(new OwnerStatus(owner, OwnerFetchStatus.Ok));
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
            CarryOverStaleOwner(owner, previous, items, statuses, OwnerFetchStatus.Error, detail);
        }
    }

    // A failed owner keeps the rows from the last snapshot rather than vanishing,
    // so a broken token does not silently empty that owner. Each carried status is
    // stamped with when the owner was last fresh; the fresh instant chains forward
    // across consecutive failures. An owner that has never been fresh (fails on its
    // first poll) carries no rows and a null instant.
    private static void CarryOverStaleOwner(
        string owner,
        QueueSnapshot? previous,
        List<QueueItem> items,
        List<OwnerStatus> statuses,
        OwnerFetchStatus status,
        string? detail
    )
    {
        statuses.Add(new OwnerStatus(owner, status, detail, LastFreshInstant(previous, owner)));

        if (previous is not null)
        {
            items.AddRange(
                previous.Items.Where(item =>
                    string.Equals(item.Identity.Owner, owner, StringComparison.OrdinalIgnoreCase)
                )
            );
        }
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
}
