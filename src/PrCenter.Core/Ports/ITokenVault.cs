namespace PrCenter.Core.Ports;

/// <summary>
/// Port for the encrypted store of one fine-grained GitHub personal access
/// token per owner. The crypto (KDF + AES) lives in the adapter and is
/// specified by a later change.
/// </summary>
public interface ITokenVault
{
    /// <summary>
    /// Stores the personal access token for an owner, encrypted at rest.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the token belongs to.</param>
    /// <param name="token">The fine-grained personal access token.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the token is stored.</returns>
    Task StoreTokenAsync(string owner, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the decrypted personal access token for an owner, or
    /// <see langword="null"/> if none is stored.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) whose token to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The decrypted token, or <see langword="null"/> if none stored.</returns>
    Task<string?> GetTokenAsync(string owner, CancellationToken cancellationToken = default);
}
