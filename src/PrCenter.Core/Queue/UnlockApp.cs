using PrCenter.Core.Ports;

namespace PrCenter.Core.Queue;

/// <summary>
/// Use case that unlocks the app and, on success, pokes the refresh trigger so
/// the first poll happens immediately rather than waiting for the poll interval.
/// The unlock result passes straight through; a failed unlock (wrong password)
/// does not poke. Unlock UI calls this rather than <see cref="IAppLock"/> directly.
/// </summary>
public sealed class UnlockApp
{
    private readonly IAppLock _appLock;
    private readonly IRefreshTrigger _trigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnlockApp"/> class.
    /// </summary>
    /// <param name="appLock">The app lock performing the unlock transition.</param>
    /// <param name="trigger">The refresh trigger poked on a successful unlock.</param>
    public UnlockApp(IAppLock appLock, IRefreshTrigger trigger)
    {
        _appLock = appLock;
        _trigger = trigger;
    }

    /// <summary>
    /// Attempts to unlock the app with the given password, poking the refresh
    /// trigger when the unlock succeeds.
    /// </summary>
    /// <param name="password">The app password to try.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the password was correct and the app is now
    /// unlocked; <see langword="false"/> if the password was wrong.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="password"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">No app password has been set (the vault is uninitialized).</exception>
    public async Task<bool> UnlockAsync(
        string password,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var unlocked = await _appLock
            .UnlockAsync(password, cancellationToken)
            .ConfigureAwait(false);
        if (unlocked)
        {
            _trigger.RequestRefresh();
        }

        return unlocked;
    }
}
