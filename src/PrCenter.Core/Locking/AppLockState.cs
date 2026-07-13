namespace PrCenter.Core.Locking;

/// <summary>
/// The app lock's runtime state, derived from whether an app password has been
/// set and whether the decrypted key is currently held in memory. Gates all
/// GitHub access, including background polling.
/// </summary>
public enum AppLockState
{
    /// <summary>No app password has been set yet; the vault must be initialized.</summary>
    Uninitialized,

    /// <summary>A password is set but the key is not in memory; the user must unlock.</summary>
    Locked,

    /// <summary>The derived key is held in memory; tokens can be read and written.</summary>
    Unlocked,
}
