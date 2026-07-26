using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Persistence.Crypto;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="ITokenVault"/> with SQLite-backed storage and
/// Argon2id + AES-GCM crypto: establishes the vault, stores and retrieves owner
/// tokens under the in-memory key, and wipes everything on reset.
/// </summary>
internal sealed partial class TokenVault : ITokenVault
{
    private const int SaltSizeBytes = 16;
    private const int KdfMemoryKib = 19456;
    private const int KdfIterations = 2;
    private const int KdfParallelism = 1;

    // A fixed known plaintext encrypted under the derived key at SetPassword and
    // decrypted on unlock: a successful AES-GCM tag proves the right password,
    // and it works even before any token is stored.
    private static readonly byte[] Sentinel = Encoding.UTF8.GetBytes("pr-center-vault-sentinel-v1");

    private readonly PrCenterDbContext _context;
    private readonly VaultKeyHolder _keyHolder;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokenVault> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenVault"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    /// <param name="keyHolder">The process-wide decrypted-key holder.</param>
    /// <param name="timeProvider">The clock stamping each token's saved instant.</param>
    /// <param name="logger">The logger for the destructive reset path.</param>
    public TokenVault(
        PrCenterDbContext context,
        VaultKeyHolder keyHolder,
        TimeProvider timeProvider,
        ILogger<TokenVault> logger
    )
    {
        _context = context;
        _keyHolder = keyHolder;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SetPasswordAsync(
        string password,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var alreadyInitialized = await _context
            .AppSecurity.AsNoTracking()
            .Where(security => security.Id == AppSecurity.SingletonId)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (alreadyInitialized)
        {
            throw new InvalidOperationException("The vault is already initialized.");
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var parameters = new KdfParameters(salt, KdfMemoryKib, KdfIterations, KdfParallelism);
        var key = Argon2KeyDeriver.DeriveKey(password, parameters);
        try
        {
            var sentinel = AesGcmCipher.Encrypt(key, Sentinel);
            _context.AppSecurity.Add(
                new AppSecurity
                {
                    Id = AppSecurity.SingletonId,
                    Salt = salt,
                    MemoryKib = KdfMemoryKib,
                    Iterations = KdfIterations,
                    Parallelism = KdfParallelism,
                    KdfVersion = AppSecurity.SupportedKdfVersion,
                    SentinelNonce = sentinel.Nonce,
                    SentinelCiphertext = sentinel.Ciphertext,
                    SentinelTag = sentinel.Tag,
                }
            );
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        // Closes the check-then-add race, but only when a row now exists: a
        // concurrent first-run that won the fixed-PK insert is an "already
        // initialized" case, whereas a genuine write failure (I/O, permissions)
        // must surface as itself rather than be mislabeled.
        catch (DbUpdateException ex)
        {
            // The failed insert is still tracked as Added; detach it so a later
            // SaveChanges on this scoped context does not retry the stale insert.
            DetachAll<AppSecurity>();

            var nowInitialized = await _context
                .AppSecurity.AsNoTracking()
                .Where(security => security.Id == AppSecurity.SingletonId)
                .AnyAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!nowInitialized)
            {
                throw;
            }

            throw new InvalidOperationException("The vault is already initialized.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="token"/> is null or whitespace.</exception>
    /// <exception cref="VaultLockedException">The vault is not unlocked.</exception>
    public async Task StoreTokenAsync(
        string owner,
        string token,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var key = _keyHolder.GetKeyOrThrow();
        var plaintextBytes = Encoding.UTF8.GetBytes(token);
        EncryptedPayload payload;
        try
        {
            payload = AesGcmCipher.Encrypt(key, plaintextBytes);
        }
        finally
        {
            // The key is an ephemeral copy from the holder; zero it (and the
            // plaintext) so no extra key/secret material lingers on the heap.
            CryptographicOperations.ZeroMemory(plaintextBytes);
            CryptographicOperations.ZeroMemory(key);
        }

        var savedAt = _timeProvider.GetUtcNow();
        var existing = await _context
            .OwnerTokens.FindAsync([owner], cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            existing.Nonce = payload.Nonce;
            existing.Ciphertext = payload.Ciphertext;
            existing.Tag = payload.Tag;
            existing.SavedAt = savedAt;
        }
        else
        {
            _context.OwnerTokens.Add(
                new OwnerToken
                {
                    Owner = owner,
                    Nonce = payload.Nonce,
                    Ciphertext = payload.Ciphertext,
                    Tag = payload.Tag,
                    SavedAt = savedAt,
                }
            );
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null or whitespace.</exception>
    /// <exception cref="VaultLockedException">The vault is not unlocked.</exception>
    public async Task<string?> GetTokenAsync(
        string owner,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        var key = _keyHolder.GetKeyOrThrow();
        try
        {
            var row = await _context
                .OwnerTokens.AsNoTracking()
                .Where(entity => entity.Owner == owner)
                .Select(entity => new
                {
                    entity.Nonce,
                    entity.Ciphertext,
                    entity.Tag,
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                return null;
            }

            byte[] plaintext;
            try
            {
                plaintext = AesGcmCipher.Decrypt(
                    key,
                    new EncryptedPayload(row.Nonce, row.Ciphertext, row.Tag)
                );
            }
            // A corrupt stored row or a key that does not match: do not leak a raw
            // crypto exception -- surface a domain-level InvalidOperationException
            // (a decrypt failure is distinct from the no-row null return above).
            // CryptographicException covers AuthenticationTagMismatchException plus
            // other AES-GCM failures (invalid key/tag size from a tampered row).
            catch (Exception ex) when (ex is CryptographicException or ArgumentException)
            {
                throw new InvalidOperationException(
                    $"The stored token for owner '{owner}' could not be decrypted; reset the vault and re-enter tokens.",
                    ex
                );
            }

            try
            {
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        finally
        {
            // Zero the ephemeral key copy on every path (including the no-row
            // return), leaving the process-wide holder's key intact.
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null or whitespace.</exception>
    /// <exception cref="VaultLockedException">The vault is not unlocked.</exception>
    public async Task DeleteTokenAsync(string owner, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        // Deleting secret material is a mutation of the token set, so it carries
        // the same unlock requirement as storing one. The key itself is not
        // needed -- only the proof that the password was entered -- so the copy
        // is zeroed immediately rather than held across the delete.
        CryptographicOperations.ZeroMemory(_keyHolder.GetKeyOrThrow());

        await _context
            .OwnerTokens.Where(entity => entity.Owner == owner)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // ExecuteDelete bypasses the change tracker, so a row tracked from an
        // earlier store in this scope would otherwise linger as stale state.
        DetachAll<OwnerToken>();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListOwnersAsync(
        CancellationToken cancellationToken = default
    )
    {
        // Plaintext key column only -- no key, no decryption, so this works while
        // the vault is Locked or Uninitialized (lock gating of polling is the app
        // lock's concern, not the vault's).
        return await _context
            .OwnerTokens.AsNoTracking()
            .Select(entity => entity.Owner)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OwnerTokenSummary>> ListOwnerTokensAsync(
        CancellationToken cancellationToken = default
    )
    {
        // Plaintext owner and saved-at columns only -- the ciphertext is never
        // projected, so this needs no key and works in every lock state.
        return await _context
            .OwnerTokens.AsNoTracking()
            .Select(entity => new OwnerTokenSummary(entity.Owner, entity.SavedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResetVaultAsync(CancellationToken cancellationToken = default)
    {
        // Both deletes run in one transaction so a failure of the second cannot
        // leave a partially-wiped vault (security row gone but tokens present, or
        // vice versa).
        await using (
            var transaction = await _context
                .Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            await _context.OwnerTokens.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _context.AppSecurity.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        // ExecuteDelete bypasses the change tracker, so any vault entity tracked
        // from a prior store in this scope is now stale (row gone in the DB).
        // Detach only the vault entities -- not the whole tracker via
        // ChangeTracker.Clear() -- so a re-store in the same scope inserts
        // cleanly, without dropping unrelated pending changes on the shared
        // scoped context. The AppSettings table is the "unrelated" entity that
        // makes this provable: a pending settings change must survive a reset,
        // which ChangeTracker.Clear() would discard.
        DetachAll<OwnerToken>();
        DetachAll<AppSecurity>();
        _keyHolder.Clear();
        LogVaultReset();
    }

    private void DetachAll<TEntity>()
        where TEntity : class
    {
        foreach (var entry in _context.ChangeTracker.Entries<TEntity>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
