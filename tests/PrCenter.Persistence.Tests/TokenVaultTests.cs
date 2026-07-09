using PrCenter.Persistence;

namespace PrCenter.Persistence.Tests;

public sealed class TokenVaultTests
{
    [Fact]
    public async Task StoreTokenAsync_WhenCalled_ThrowsNotImplemented()
    {
        // Arrange
        var vault = new TokenVault();

        // Act / Assert
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            vault.StoreTokenAsync("owner", "token", CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetTokenAsync_WhenCalled_ThrowsNotImplemented()
    {
        // Arrange
        var vault = new TokenVault();

        // Act / Assert
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            vault.GetTokenAsync("owner", CancellationToken.None)
        );
    }
}
