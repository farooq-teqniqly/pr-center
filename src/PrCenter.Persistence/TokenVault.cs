using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PrCenter.Core.Ports;
using PrCenter.Persistence.Crypto;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="ITokenVault"/> with SQLite-backed storage and
/// Argon2id + AES-GCM crypto. Token store/retrieve arrive with a later task;
/// this task establishes the vault via <see cref="SetPasswordAsync"/>.
/// </summary>
internal sealed class TokenVault : ITokenVault
{
    private const int SaltSizeBytes = 16;
    private const int KdfMemoryKib = 19456;
    private const int KdfIterations = 2;
    private const int KdfParallelism = 1;
    private const int CurrentKdfVersion = 1;
    private const int SecurityRowId = 1;

    // A fixed known plaintext encrypted under the derived key at SetPassword and
    // decrypted on unlock: a successful AES-GCM tag proves the right password,
    // and it works even before any token is stored.
    private static readonly byte[] Sentinel = Encoding.UTF8.GetBytes("pr-center-vault-sentinel-v1");

    private readonly PrCenterDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenVault"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    public TokenVault(PrCenterDbContext context)
    {
        _context = context;
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
                    Id = SecurityRowId,
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
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

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
