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
            .Where(security => security.Id == AppSecurity.SingletonId)
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

        // Idempotent when already unlocked: no re-derivation, no re-key. Unlock
        // is a transition from Locked, so an unlock while Unlocked is a no-op
        // success -- access is already granted for this process.
        if (_keyHolder.HasKey)
        {
            return true;
        }

        var security =
            await _context
                .AppSecurity.AsNoTracking()
                .Where(row => row.Id == AppSecurity.SingletonId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException("No app password has been set.");

        if (security.KdfVersion != AppSecurity.SupportedKdfVersion)
        {
            throw new InvalidOperationException(
                $"The stored KDF version {security.KdfVersion} is not supported by this build."
            );
        }

        var parameters = new KdfParameters(
            security.Salt,
            security.MemoryKib,
            security.Iterations,
            security.Parallelism
        );
        var sentinel = new EncryptedPayload(
            security.SentinelNonce,
            security.SentinelCiphertext,
            security.SentinelTag
        );

        byte[]? key = null;
        var unlocked = false;
        try
        {
            // Derivation is inside the protected region so a corrupt/tampered row
            // (bad KDF params or sentinel length) cannot escape as an unhandled
            // exception with a live key left in memory.
            key = Argon2KeyDeriver.DeriveKey(password, parameters);

            // The plaintext is the known sentinel, but zero it anyway to keep the
            // "decrypted buffers are cleared by the caller" pattern consistent.
            var sentinelPlaintext = AesGcmCipher.Decrypt(key, sentinel);
            CryptographicOperations.ZeroMemory(sentinelPlaintext);

            _keyHolder.SetKey(key);
            unlocked = true;
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            // The tag is the password check: a mismatch is a wrong password.
            LogUnlockFailed();
            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            // Bad KDF params or a corrupt-length sentinel: the stored row is
            // unusable. Surface a clear domain error instead of a raw crypto or
            // argument exception (and not a wrong-password false).
            throw new InvalidOperationException("The stored vault data is invalid or corrupt.", ex);
        }
        finally
        {
            // Zero the ephemeral derived key on every path that did not hand it to
            // the holder (wrong password, or a corrupt-row exception propagating).
            if (!unlocked && key is not null)
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
