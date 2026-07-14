using Microsoft.Extensions.Logging;
using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;

namespace PrCenter.Core.Queue;

/// <summary>
/// Use case that refreshes the review queue: it enumerates the owners with a
/// stored token, and for each owner resolves the authenticated login, fetches
/// that owner's review-queue facts, and derives the shown queue items against the
/// stored last-seen markers -- everything evaluated relative to the user. It then
/// publishes a new snapshot of the derived items and each owner's fetch status. A
/// per-owner fetch failure degrades only that owner; a locked vault mid-poll
/// aborts the whole refresh without touching the previously published snapshot.
/// </summary>
public sealed partial class RefreshQueue
{
    private readonly ITokenVault _vault;
    private readonly IGitHubFacts _facts;
    private readonly IStateStore _stateStore;
    private readonly QueueSnapshotHolder _holder;
    private readonly ILogger<RefreshQueue> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshQueue"/> class.
    /// </summary>
    /// <param name="vault">The vault enumerating the owners to poll.</param>
    /// <param name="facts">The GitHub facts port for login resolution and fetches.</param>
    /// <param name="stateStore">The store of per-pull-request last-seen markers.</param>
    /// <param name="holder">The holder the refreshed snapshot is published into.</param>
    /// <param name="logger">The logger for the aborted-poll warning path.</param>
    public RefreshQueue(
        ITokenVault vault,
        IGitHubFacts facts,
        IStateStore stateStore,
        QueueSnapshotHolder holder,
        ILogger<RefreshQueue> logger
    )
    {
        _vault = vault;
        _facts = facts;
        _stateStore = stateStore;
        _holder = holder;
        _logger = logger;
    }

    /// <summary>
    /// Runs one queue refresh and publishes the resulting snapshot.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the snapshot has been published.</returns>
    /// <exception cref="VaultLockedException">
    /// The vault locked mid-poll; the refresh is abandoned and the previously
    /// published snapshot is left untouched.
    /// </exception>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var owners = await _vault.ListOwnersAsync(cancellationToken).ConfigureAwait(false);

        var items = new List<QueueItem>();
        var statuses = new List<OwnerStatus>();
        try
        {
            foreach (var owner in owners)
            {
                await RefreshOwnerAsync(owner, items, statuses, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        // A locked vault is a global precondition failure, not a per-owner one:
        // abandon the whole refresh so the last good snapshot survives, but log it
        // (baseline: no silent catch) before letting the loop resume waiting.
        catch (VaultLockedException ex)
        {
            LogVaultLockedDuringRefresh(ex);
            throw;
        }

        _holder.Publish(items, statuses);
    }

    private async Task RefreshOwnerAsync(
        string owner,
        List<QueueItem> items,
        List<OwnerStatus> statuses,
        CancellationToken cancellationToken
    )
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
            statuses.Add(new OwnerStatus(owner, result.Status, result.Detail));
            return;
        }

        foreach (var facts in result.Facts)
        {
            var item = await DeriveItemAsync(facts, myLogin, cancellationToken)
                .ConfigureAwait(false);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        statuses.Add(new OwnerStatus(owner, OwnerFetchStatus.Ok));
    }

    private async Task<QueueItem?> DeriveItemAsync(
        PullRequestFacts facts,
        string myLogin,
        CancellationToken cancellationToken
    )
    {
        var lastSeen = await _stateStore
            .GetLastSeenAsync(facts.Identity.Id, cancellationToken)
            .ConfigureAwait(false);
        return QueueItemDeriver.Derive(facts, myLogin, lastSeen);
    }
}
