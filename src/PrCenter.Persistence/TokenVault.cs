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
    private const int CurrentKdfVersion = 1;

    // A fixed known plaintext encrypted under the derived key at SetPassword and
    // decrypted on unlock: a successful AES-GCM tag proves the right password,
    // and it works even before any token is stored.
    private static readonly byte[] Sentinel = Encoding.UTF8.GetBytes("pr-center-vault-sentinel-v1");

    private readonly PrCenterDbContext _context;
    private readonly VaultKeyHolder _keyHolder;
    private readonly ILogger<TokenVault> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenVault"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    /// <param name="keyHolder">The process-wide decrypted-key holder.</param>
    /// <param name="logger">The logger for the destructive reset path.</param>
    public TokenVault(
        PrCenterDbContext context,
        VaultKeyHolder keyHolder,
        ILogger<TokenVault> logger
    )
    {
        _context = context;
        _keyHolder = keyHolder;
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
                    KdfVersion = CurrentKdfVersion,
                    SentinelNonce = sentinel.Nonce,
                    SentinelCiphertext = sentinel.Ciphertext,
                    SentinelTag = sentinel.Tag,
                }
            );
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        // Closes the check-then-add race: if a concurrent first-run won the
        // fixed-PK insert, this one fails and is reported as already initialized.
        catch (DbUpdateException ex)
        {
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
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }

        var existing = await _context
            .OwnerTokens.FindAsync([owner], cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            existing.Nonce = payload.Nonce;
            existing.Ciphertext = payload.Ciphertext;
            existing.Tag = payload.Tag;
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

        var plaintext = AesGcmCipher.Decrypt(
            key,
            new EncryptedPayload(row.Nonce, row.Ciphertext, row.Tag)
        );
        try
        {
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <inheritdoc />
    public async Task ResetVaultAsync(CancellationToken cancellationToken = default)
    {
        await _context.OwnerTokens.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _context.AppSecurity.ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // ExecuteDelete bypasses the change tracker, so any entity tracked from a
        // prior store in this scope is now stale (row gone in the DB). Detach
        // everything so a re-store in the same scope inserts cleanly instead of
        // updating a phantom row.
        _context.ChangeTracker.Clear();
        _keyHolder.Clear();
        LogVaultReset();
    }
}
