using System.Security.Cryptography;
using PrCenter.Core.Locking;

namespace PrCenter.Persistence;

/// <summary>
/// Holds the decrypted vault key in memory for the life of the process, shared
/// across all Blazor circuits and tabs. Registered as a singleton; there is no
/// idle auto-lock, so the key is discarded only when the process stops (the
/// container disposes the singleton) or the vault is reset. The key is zeroed
/// on clear, on re-key, and on dispose as a best effort.
/// </summary>
internal sealed class VaultKeyHolder : IDisposable
{
    private readonly Lock _gate = new();
    private byte[]? _key;

    /// <summary>Gets a value indicating whether a decrypted key is currently held.</summary>
    public bool HasKey
    {
        get
        {
            lock (_gate)
            {
                return _key is not null;
            }
        }
    }

    /// <summary>
    /// Stores the decrypted key, zeroing and replacing any existing one.
    /// </summary>
    /// <param name="key">The decrypted vault key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public void SetKey(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_gate)
        {
            if (_key is not null)
            {
                CryptographicOperations.ZeroMemory(_key);
            }

            _key = key;
        }
    }

    /// <summary>
    /// Gets an ephemeral copy of the held key, or throws when the vault is locked.
    /// A copy is returned so a concurrent <see cref="Clear"/> -- which zeroes the
    /// internal array in place -- cannot corrupt a key already handed to a caller
    /// that runs crypto outside this lock.
    /// </summary>
    /// <returns>A copy of the decrypted vault key.</returns>
    /// <exception cref="VaultLockedException">No key is currently held.</exception>
    public byte[] GetKeyOrThrow()
    {
        lock (_gate)
        {
            return (_key ?? throw new VaultLockedException()).ToArray();
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

    /// <inheritdoc />
    public void Dispose() => Clear();
}
