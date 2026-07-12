using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Persistence.Crypto;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IAppLock"/>. Derives the lock state from the
/// presence of the app-security row (has a password been set?) and the
/// in-memory key holder (is the key held?), and performs the unlock transition.
/// </summary>
internal sealed partial class AppLock : IAppLock
{
    private readonly PrCenterDbContext _context;
    private readonly VaultKeyHolder _keyHolder;
    private readonly ILogger<AppLock> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppLock"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    /// <param name="keyHolder">The process-wide decrypted-key holder.</param>
    /// <param name="logger">The logger for failed-unlock diagnostics.</param>
    public AppLock(PrCenterDbContext context, VaultKeyHolder keyHolder, ILogger<AppLock> logger)
    {
        _context = context;
        _keyHolder = keyHolder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AppLockState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var initialized = await _context
            .AppSecurity.AsNoTracking()
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!initialized)
        {
            return AppLockState.Uninitialized;
        }

        return _keyHolder.HasKey ? AppLockState.Unlocked : AppLockState.Locked;
    }

    /// <inheritdoc />
    public async Task<bool> UnlockAsync(
        string password,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var security =
            await _context
                .AppSecurity.AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("No app password has been set.");

        var parameters = new KdfParameters(
            security.Salt,
            security.MemoryKib,
            security.Iterations,
            security.Parallelism
        );
        var key = Argon2KeyDeriver.DeriveKey(password, parameters);
        var sentinel = new EncryptedPayload(
            security.SentinelNonce,
            security.SentinelCiphertext,
            security.SentinelTag
        );
        try
        {
            AesGcmCipher.Decrypt(key, sentinel);
        }
        catch (AuthenticationTagMismatchException)
        {
            CryptographicOperations.ZeroMemory(key);
            LogUnlockFailed();
            return false;
        }

        _keyHolder.SetKey(key);
        return true;
    }
}
