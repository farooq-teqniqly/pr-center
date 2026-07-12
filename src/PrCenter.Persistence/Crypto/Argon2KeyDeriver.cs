using System.Text;
using Konscious.Security.Cryptography;

namespace PrCenter.Persistence.Crypto;

/// <summary>
/// Derives the 32-byte (AES-256) vault key from the app password and the stored
/// Argon2id parameters. Argon2id is memory-hard, chosen to resist offline GPU
/// cracking of the single app password that guards the at-rest tokens.
/// </summary>
internal static class Argon2KeyDeriver
{
    /// <summary>The derived key length in bytes (AES-256).</summary>
    internal const int KeyLengthBytes = 32;

    /// <summary>
    /// Derives the vault key from the password and parameters.
    /// </summary>
    /// <param name="password">The app password.</param>
    /// <param name="parameters">The stored Argon2id parameters.</param>
    /// <returns>The derived 32-byte key.</returns>
    /// <exception cref="ArgumentException"><paramref name="password"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is null.</exception>
    public static byte[] DeriveKey(string password, KdfParameters parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(parameters);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = parameters.Salt,
            MemorySize = parameters.MemoryKib,
            Iterations = parameters.Iterations,
            DegreeOfParallelism = parameters.Parallelism,
        };
        return argon2.GetBytes(KeyLengthBytes);
    }
}
