using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="ITokenVault"/> with SQLite-backed storage
/// and KDF + AES crypto. A skeleton stub for now: members throw
/// <see cref="NotImplementedException"/> until the token-vault change specifies
/// their behavior.
/// </summary>
internal sealed class TokenVault : ITokenVault
{
    /// <inheritdoc />
    public Task StoreTokenAsync(
        string owner,
        string token,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<string?> GetTokenAsync(
        string owner,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();
}
