namespace PrCenter.Persistence;

/// <summary>
/// The single-row record establishing the vault: the Argon2id salt and
/// parameters used to derive the encryption key from the app password, plus an
/// AES-GCM-encrypted known sentinel used to verify an entered password. The
/// password itself is never stored. The existence of this row is what
/// distinguishes an uninitialized vault from a configured (locked) one.
/// </summary>
internal sealed class AppSecurity
{
    /// <summary>Gets or sets the fixed single-row primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the random salt fed to Argon2id alongside the password.</summary>
    public byte[] Salt { get; set; } = null!;

    /// <summary>Gets or sets the Argon2id memory cost in kibibytes.</summary>
    public int MemoryKib { get; set; }

    /// <summary>Gets or sets the Argon2id iteration count.</summary>
    public int Iterations { get; set; }

    /// <summary>Gets or sets the Argon2id degree of parallelism.</summary>
    public int Parallelism { get; set; }

    /// <summary>Gets or sets the KDF-format version, so parameters can change without a data break.</summary>
    public int KdfVersion { get; set; }

    /// <summary>Gets or sets the random AES-GCM nonce used to encrypt the sentinel.</summary>
    public byte[] SentinelNonce { get; set; } = null!;

    /// <summary>Gets or sets the AES-GCM ciphertext of the known sentinel.</summary>
    public byte[] SentinelCiphertext { get; set; } = null!;

    /// <summary>Gets or sets the AES-GCM authentication tag for the sentinel ciphertext.</summary>
    public byte[] SentinelTag { get; set; } = null!;
}
