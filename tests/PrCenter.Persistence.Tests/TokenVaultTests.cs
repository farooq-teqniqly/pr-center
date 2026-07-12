using Microsoft.EntityFrameworkCore;
using PrCenter.Persistence;

namespace PrCenter.Persistence.Tests;

public sealed class TokenVaultTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task SetPasswordAsync_FirstRun_WritesSecurityRow()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = new TokenVault(context);

        // Act
        await vault.SetPasswordAsync("correct horse", CancellationToken.None);

        // Assert
        var row = await context.AppSecurity.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.Equal(16, row.Salt.Length);
        Assert.NotEmpty(row.SentinelNonce);
        Assert.NotEmpty(row.SentinelCiphertext);
        Assert.NotEmpty(row.SentinelTag);
    }

    [Fact]
    public async Task SetPasswordAsync_WhenAlreadyInitialized_Throws()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = new TokenVault(context);
        await vault.SetPasswordAsync("first", CancellationToken.None);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            vault.SetPasswordAsync("second", CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetPasswordAsync_NullOrWhitespacePassword_Throws(string? password)
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = new TokenVault(context);

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            vault.SetPasswordAsync(password!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreTokenAsync_WhenCalled_ThrowsNotImplemented()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = new TokenVault(context);

        // Act / Assert
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            vault.StoreTokenAsync("owner", "token", CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetTokenAsync_WhenCalled_ThrowsNotImplemented()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = new TokenVault(context);

        // Act / Assert
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            vault.GetTokenAsync("owner", CancellationToken.None)
        );
    }

    public void Dispose() => _database.Dispose();
}
