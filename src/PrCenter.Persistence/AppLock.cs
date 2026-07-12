using Microsoft.EntityFrameworkCore;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IAppLock"/>. Derives the lock state from the
/// presence of the app-security row (has a password been set?) and the
/// in-memory key holder (is the key held?).
/// </summary>
internal sealed class AppLock : IAppLock
{
    private readonly PrCenterDbContext _context;
    private readonly VaultKeyHolder _keyHolder;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppLock"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    /// <param name="keyHolder">The process-wide decrypted-key holder.</param>
    public AppLock(PrCenterDbContext context, VaultKeyHolder keyHolder)
    {
        _context = context;
        _keyHolder = keyHolder;
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
}
