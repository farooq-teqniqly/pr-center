using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

namespace PrCenter.Core.Settings;

/// <summary>
/// Use case for first-run setup: sets the app password and, on success, unlocks
/// the app and pokes the refresh trigger. Composed here rather than in the UI
/// because <see cref="ITokenVault.SetPasswordAsync"/> deliberately does not
/// unlock -- without this composition the user who just chose a password would
/// be sent to the unlock card to type it again. Setup UI calls this rather than
/// the vault and the lock directly.
/// </summary>
public sealed class InitializeVault
{
    private readonly ITokenVault _vault;
    private readonly IAppLock _appLock;
    private readonly IRefreshTrigger _trigger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeVault"/> class.
    /// </summary>
    /// <param name="vault">The vault establishing the app password.</param>
    /// <param name="appLock">The app lock performing the unlock transition.</param>
    /// <param name="trigger">The refresh trigger poked once the app is unlocked.</param>
    public InitializeVault(ITokenVault vault, IAppLock appLock, IRefreshTrigger trigger)
    {
        _vault = vault;
        _appLock = appLock;
        _trigger = trigger;
    }

    /// <summary>
    /// Sets the app password and unlocks the app with it, poking the refresh
    /// trigger when the unlock succeeds.
    /// </summary>
    /// <param name="password">The app password to establish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the vault was established and the app is now
    /// unlocked; <see langword="false"/> when the password was set but the unlock
    /// did not take, leaving the app Locked.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="password"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The vault is already initialized.</exception>
    public async Task<bool> InitializeAsync(
        string password,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await _vault.SetPasswordAsync(password, cancellationToken).ConfigureAwait(false);

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
