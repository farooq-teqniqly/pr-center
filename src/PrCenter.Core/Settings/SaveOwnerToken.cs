using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

namespace PrCenter.Core.Settings;

/// <summary>
/// Use case for adding an owner or replacing its token: stores the token and
/// pokes the refresh trigger so the owner's fetch status arrives with the next
/// poll rather than after the interval. Adding an owner is storing its token --
/// there is no separate owner record. Settings UI calls this rather than the
/// vault directly, so the poke cannot be forgotten by a future caller.
/// </summary>
public sealed class SaveOwnerToken
{
    private readonly ITokenVault _vault;
    private readonly IRefreshTrigger _trigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveOwnerToken"/> class.
    /// </summary>
    /// <param name="vault">The vault storing the token.</param>
    /// <param name="trigger">The refresh trigger poked after a successful store.</param>
    public SaveOwnerToken(ITokenVault vault, IRefreshTrigger trigger)
    {
        _vault = vault;
        _trigger = trigger;
    }

    /// <summary>
    /// Stores an owner's token, replacing any token already stored for it, and
    /// pokes the refresh trigger on success.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the token belongs to.</param>
    /// <param name="token">The fine-grained personal access token.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the token is stored.</returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="token"/> is null or whitespace.</exception>
    /// <exception cref="Locking.VaultLockedException">The vault is not unlocked.</exception>
    public async Task SaveAsync(
        string owner,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        await _vault.StoreTokenAsync(owner, token, cancellationToken).ConfigureAwait(false);
        _trigger.RequestRefresh();
    }
}
