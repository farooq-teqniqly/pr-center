using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

namespace PrCenter.Core.Settings;

/// <summary>
/// Use case for removing an owner: deletes that owner's token and pokes the
/// refresh trigger, so the next snapshot drops the owner's items and status
/// rather than carrying a removed owner until the interval elapses.
/// </summary>
public sealed class RemoveOwner
{
    private readonly ITokenVault _vault;
    private readonly IRefreshTrigger _trigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveOwner"/> class.
    /// </summary>
    /// <param name="vault">The vault deleting the token.</param>
    /// <param name="trigger">The refresh trigger poked after a successful delete.</param>
    public RemoveOwner(ITokenVault vault, IRefreshTrigger trigger)
    {
        _vault = vault;
        _trigger = trigger;
    }

    /// <summary>
    /// Deletes an owner's stored token and pokes the refresh trigger on success.
    /// Removing an owner that has no stored token succeeds and still pokes, since
    /// the caller's intent -- "this owner should not be polled" -- now holds.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the owner is removed.</returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null or whitespace.</exception>
    /// <exception cref="Locking.VaultLockedException">The vault is not unlocked.</exception>
    public async Task RemoveAsync(string owner, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        await _vault.DeleteTokenAsync(owner, cancellationToken).ConfigureAwait(false);
        _trigger.RequestRefresh();
    }
}
