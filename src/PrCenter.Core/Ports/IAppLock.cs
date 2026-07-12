using PrCenter.Core.Locking;

namespace PrCenter.Core.Ports;

/// <summary>
/// Port for the app lock: the runtime gate that decides whether the vault is
/// uninitialized, locked, or unlocked. The unlock transition is added by a
/// later change.
/// </summary>
public interface IAppLock
{
    /// <summary>
    /// Gets the current lock state, derived from whether an app password has
    /// been set and whether the decrypted key is held in memory.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The current <see cref="AppLockState"/>.</returns>
    Task<AppLockState> GetStateAsync(CancellationToken cancellationToken = default);
}
