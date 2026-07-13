using PrCenter.Core.Locking;

namespace PrCenter.Core.Ports;

/// <summary>
/// Port for the encrypted store of one fine-grained GitHub personal access
/// token per owner. The crypto (KDF + AES) lives in the adapter and is
/// specified by a later change.
/// </summary>
public interface ITokenVault
{
    /// <summary>
    /// Sets the app password for the first time, establishing the vault: derives
    /// the encryption key, stores the salt and KDF parameters, and stores an
    /// encrypted sentinel used to verify the password on unlock. Does not unlock
    /// the vault; the user unlocks separately.
    /// </summary>
    /// <param name="password">The app password to establish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the vault is initialized.</returns>
    /// <exception cref="ArgumentException"><paramref name="password"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The vault is already initialized.</exception>
    Task SetPasswordAsync(string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the personal access token for an owner, encrypted at rest.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the token belongs to.</param>
    /// <param name="token">The fine-grained personal access token.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the token is stored.</returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="token"/> is null or whitespace.</exception>
    /// <exception cref="VaultLockedException">The vault is not unlocked.</exception>
    Task StoreTokenAsync(string owner, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the decrypted personal access token for an owner, or
    /// <see langword="null"/> if none is stored.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) whose token to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decrypted token, or <see langword="null"/> if none stored.</returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null or whitespace.</exception>
    /// <exception cref="VaultLockedException">The vault is not unlocked.</exception>
    /// <exception cref="InvalidOperationException">The stored token cannot be decrypted (corrupt row or a key that does not match).</exception>
    Task<string?> GetTokenAsync(string owner, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wipes the vault: deletes every stored owner token and the app-security
    /// row, and discards the in-memory key. Does not require the vault to be
    /// unlocked. There is no recovery -- the user must set a new password and
    /// re-enter tokens.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the vault is wiped.</returns>
    Task ResetVaultAsync(CancellationToken cancellationToken = default);
}
