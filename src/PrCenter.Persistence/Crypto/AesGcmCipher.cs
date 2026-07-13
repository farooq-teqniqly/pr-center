using System.Security.Cryptography;

namespace PrCenter.Persistence.Crypto;

/// <summary>
/// Authenticated encryption of vault secrets with AES-256-GCM. Each encryption
/// draws a fresh random nonce; decryption fails on a wrong key or tampering
/// because the authentication tag will not verify.
/// </summary>
internal static class AesGcmCipher
{
    // Fixed 12-byte nonce per design D2 -- the standard AES-GCM size. Named
    // explicitly rather than via NonceByteSizes.MaxSize so the stored format
    // stays pinned to the documented value.
    private const int NonceSizeBytes = 12;

    /// <summary>
    /// Encrypts plaintext under the key with a fresh random nonce.
    /// </summary>
    /// <param name="key">The 32-byte AES-256 key.</param>
    /// <param name="plaintext">The bytes to encrypt.</param>
    /// <returns>The nonce, ciphertext, and authentication tag.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="plaintext"/> is null.</exception>
    public static EncryptedPayload Encrypt(byte[] key, byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(plaintext);

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return new EncryptedPayload(nonce, ciphertext, tag);
    }

    /// <summary>
    /// Decrypts and verifies a payload under the key.
    /// </summary>
    /// <param name="key">The 32-byte AES-256 key.</param>
    /// <param name="payload">The nonce, ciphertext, and tag to decrypt.</param>
    /// <returns>The decrypted plaintext.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="payload"/> is null.</exception>
    /// <exception cref="System.Security.Cryptography.AuthenticationTagMismatchException">The key is wrong or the payload was tampered with.</exception>
    public static byte[] Decrypt(byte[] key, EncryptedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(payload);

        var plaintext = new byte[payload.Ciphertext.Length];
        using var aes = new AesGcm(key, payload.Tag.Length);
        aes.Decrypt(payload.Nonce, payload.Ciphertext, payload.Tag, plaintext);
        return plaintext;
    }
}
