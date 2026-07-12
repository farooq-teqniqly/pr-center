using System.Security.Cryptography;
using PrCenter.Core.Locking;

namespace PrCenter.Persistence;

/// <summary>
/// Holds the decrypted vault key in memory for the life of the process, shared
/// across all Blazor circuits and tabs. Registered as a singleton; there is no
/// idle auto-lock, so the key is discarded only when the process stops or the
/// vault is reset (or cleared here). The key is zeroed on clear as a best effort.
/// </summary>
internal sealed class VaultKeyHolder
{
    private readonly Lock _gate = new();
    private byte[]? _key;

    /// <summary>Gets a value indicating whether a decrypted key is currently held.</summary>
    public bool HasKey => _key is not null;

    /// <summary>
    /// Stores the decrypted key, replacing any existing one.
    /// </summary>
    /// <param name="key">The decrypted vault key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public void SetKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_gate)
        {
            _key = key;
        }
    }

    /// <summary>
    /// Gets the held key, or throws when the vault is locked.
    /// </summary>
    /// <returns>The decrypted vault key.</returns>
    /// <exception cref="VaultLockedException">No key is currently held.</exception>
    public byte[] GetKeyOrThrow()
    {
        lock (_gate)
        {
            return _key ?? throw new VaultLockedException();
        }
    }

    /// <summary>Zeroes and discards the held key, if any.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (_key is not null)
            {
                CryptographicOperations.ZeroMemory(_key);
                _key = null;
            }
        }
    }
}
