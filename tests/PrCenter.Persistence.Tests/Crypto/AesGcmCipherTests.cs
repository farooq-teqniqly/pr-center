using System.Security.Cryptography;
using System.Text;
using PrCenter.Persistence.Crypto;

namespace PrCenter.Persistence.Tests.Crypto;

public sealed class AesGcmCipherTests
{
    private static byte[] Key() => RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Decrypt_OfEncryptedPayload_RoundTripsPlaintext()
    {
        // Arrange
        var key = Key();
        var plaintext = Encoding.UTF8.GetBytes("github_pat_secret");

        // Act
        var payload = AesGcmCipher.Encrypt(key, plaintext);
        var decrypted = AesGcmCipher.Decrypt(key, payload);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsAuthenticationTagMismatch()
    {
        // Arrange
        var payload = AesGcmCipher.Encrypt(Key(), Encoding.UTF8.GetBytes("secret"));

        // Act / Assert
        Assert.Throws<AuthenticationTagMismatchException>(() =>
            AesGcmCipher.Decrypt(Key(), payload)
        );
    }

    [Fact]
    public void Encrypt_CalledTwiceForSamePlaintext_UsesDifferentNonce()
    {
        // Arrange
        var key = Key();
        var plaintext = Encoding.UTF8.GetBytes("secret");

        // Act
        var first = AesGcmCipher.Encrypt(key, plaintext);
        var second = AesGcmCipher.Encrypt(key, plaintext);

        // Assert
        Assert.NotEqual(first.Nonce, second.Nonce);
        Assert.NotEqual(first.Ciphertext, second.Ciphertext);
    }

    [Fact]
    public void Encrypt_NullKey_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => AesGcmCipher.Encrypt(null!, [1, 2, 3]));
    }

    [Fact]
    public void Encrypt_NullPlaintext_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => AesGcmCipher.Encrypt(Key(), null!));
    }

    [Fact]
    public void Decrypt_NullKey_Throws()
    {
        // Arrange
        var payload = new EncryptedPayload([1], [2], [3]);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => AesGcmCipher.Decrypt(null!, payload));
    }

    [Fact]
    public void Decrypt_NullPayload_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => AesGcmCipher.Decrypt(Key(), null!));
    }
}
