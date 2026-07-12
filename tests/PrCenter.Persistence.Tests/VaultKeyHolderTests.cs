using PrCenter.Core.Locking;
using PrCenter.Persistence;

namespace PrCenter.Persistence.Tests;

public sealed class VaultKeyHolderTests
{
    [Fact]
    public void HasKey_WhenNoKeySet_ReturnsFalse()
    {
        // Arrange
        var holder = new VaultKeyHolder();

        // Act / Assert
        Assert.False(holder.HasKey);
    }

    [Fact]
    public void HasKey_AfterSetKey_ReturnsTrue()
    {
        // Arrange
        var holder = new VaultKeyHolder();

        // Act
        holder.SetKey([1, 2, 3]);

        // Assert
        Assert.True(holder.HasKey);
    }

    [Fact]
    public void GetKeyOrThrow_AfterSetKey_ReturnsTheKey()
    {
        // Arrange
        var holder = new VaultKeyHolder();
        var key = new byte[] { 9, 8, 7 };

        // Act
        holder.SetKey(key);

        // Assert
        Assert.Equal(key, holder.GetKeyOrThrow());
    }

    [Fact]
    public void GetKeyOrThrow_WhenNoKeySet_ThrowsVaultLocked()
    {
        // Arrange
        var holder = new VaultKeyHolder();

        // Act / Assert
        Assert.Throws<VaultLockedException>(() => holder.GetKeyOrThrow());
    }

    [Fact]
    public void SetKey_NullKey_Throws()
    {
        // Arrange
        var holder = new VaultKeyHolder();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => holder.SetKey(null!));
    }

    [Fact]
    public void Clear_AfterSetKey_LeavesNoKeyHeld()
    {
        // Arrange
        var holder = new VaultKeyHolder();
        holder.SetKey([1, 2, 3]);

        // Act
        holder.Clear();

        // Assert
        Assert.False(holder.HasKey);
    }

    [Fact]
    public void Clear_ZeroesTheHeldKeyBytes()
    {
        // Arrange
        var holder = new VaultKeyHolder();
        var key = new byte[] { 1, 2, 3 };
        holder.SetKey(key);

        // Act
        holder.Clear();

        // Assert
        Assert.Equal(new byte[] { 0, 0, 0 }, key);
    }
}
