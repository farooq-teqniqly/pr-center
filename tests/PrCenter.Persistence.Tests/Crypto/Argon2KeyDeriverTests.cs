using PrCenter.Persistence.Crypto;

namespace PrCenter.Persistence.Tests.Crypto;

public sealed class Argon2KeyDeriverTests
{
    private static KdfParameters SmallParameters() =>
        new(Salt: new byte[16], MemoryKib: 1024, Iterations: 1, Parallelism: 1);

    [Fact]
    public void DeriveKey_SamePasswordAndParameters_ProducesSameKey()
    {
        // Arrange
        var parameters = SmallParameters();

        // Act
        var first = Argon2KeyDeriver.DeriveKey("correct horse", parameters);
        var second = Argon2KeyDeriver.DeriveKey("correct horse", parameters);

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void DeriveKey_DifferentPassword_ProducesDifferentKey()
    {
        // Arrange
        var parameters = SmallParameters();

        // Act
        var fromOne = Argon2KeyDeriver.DeriveKey("password one", parameters);
        var fromTwo = Argon2KeyDeriver.DeriveKey("password two", parameters);

        // Assert
        Assert.NotEqual(fromOne, fromTwo);
    }

    [Fact]
    public void DeriveKey_WhenCalled_ReturnsThirtyTwoByteKey()
    {
        // Arrange
        var parameters = SmallParameters();

        // Act
        var key = Argon2KeyDeriver.DeriveKey("any password", parameters);

        // Assert
        Assert.Equal(32, key.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveKey_NullOrWhitespacePassword_Throws(string? password)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            Argon2KeyDeriver.DeriveKey(password!, SmallParameters())
        );
    }

    [Fact]
    public void DeriveKey_NullParameters_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Argon2KeyDeriver.DeriveKey("password", null!));
    }
}
