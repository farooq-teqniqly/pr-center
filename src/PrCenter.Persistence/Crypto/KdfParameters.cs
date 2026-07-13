namespace PrCenter.Persistence.Crypto;

/// <summary>
/// The Argon2id inputs, other than the password, needed to derive the vault
/// encryption key. Persisted on the app-security row so the key can be
/// re-derived on unlock with the exact parameters it was created with.
/// </summary>
/// <param name="Salt">The random salt combined with the password.</param>
/// <param name="MemoryKib">The Argon2id memory cost, in kibibytes.</param>
/// <param name="Iterations">The Argon2id iteration count.</param>
/// <param name="Parallelism">The Argon2id degree of parallelism.</param>
internal sealed record KdfParameters(byte[] Salt, int MemoryKib, int Iterations, int Parallelism);
