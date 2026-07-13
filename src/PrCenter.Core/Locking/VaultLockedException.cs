namespace PrCenter.Core.Locking;

/// <summary>
/// Thrown when an operation that needs the decrypted vault key is attempted
/// while the vault is not unlocked. Serves as defense in depth behind the
/// upstream lock gating.
/// </summary>
public sealed class VaultLockedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="VaultLockedException"/> class.</summary>
    public VaultLockedException()
        : base("The vault is locked; unlock it before accessing tokens.") { }

    /// <summary>Initializes a new instance of the <see cref="VaultLockedException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    public VaultLockedException(string message)
        : base(message) { }

    /// <summary>Initializes a new instance of the <see cref="VaultLockedException"/> class.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public VaultLockedException(string message, Exception innerException)
        : base(message, innerException) { }
}
