using PrCenter.Core.Locking;

namespace PrCenter.Core.Ports;

/// <summary>
/// Port for the app lock: the runtime gate that decides whether the vault is
/// uninitialized, locked, or unlocked, and performs the unlock transition.
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

    /// <summary>
    /// Attempts to unlock the vault with the given password: re-derives the key
    /// and verifies it against the stored sentinel, holding the key in memory on
    /// success.
    /// </summary>
    /// <param name="password">The app password to try.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the password was correct and the vault is now
    /// unlocked; <see langword="false"/> if the password was wrong.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="password"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">No app password has been set (the vault is uninitialized).</exception>
    Task<bool> UnlockAsync(string password, CancellationToken cancellationToken = default);
}
