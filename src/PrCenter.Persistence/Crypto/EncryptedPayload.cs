namespace PrCenter.Persistence.Crypto;

/// <summary>
/// The output of an AES-GCM encryption: the random nonce, the ciphertext, and
/// the authentication tag. All three are needed to decrypt and verify.
/// </summary>
/// <param name="Nonce">The random nonce used for this encryption.</param>
/// <param name="Ciphertext">The encrypted bytes.</param>
/// <param name="Tag">The authentication tag over the ciphertext.</param>
internal sealed record EncryptedPayload(byte[] Nonce, byte[] Ciphertext, byte[] Tag);
