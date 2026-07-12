using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PrCenter.Core.Locking;
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
        var vault = CreateVault(context, new VaultKeyHolder());

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
        var vault = CreateVault(context, new VaultKeyHolder());
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
        var vault = CreateVault(context, new VaultKeyHolder());

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            vault.SetPasswordAsync(password!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreTokenAsync_ThenGetTokenAsync_ReturnsTheToken()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());

        // Act
        await vault.StoreTokenAsync("PerfectServe", "github_pat_abc", CancellationToken.None);
        var token = await vault.GetTokenAsync("PerfectServe", CancellationToken.None);

        // Assert
        Assert.Equal("github_pat_abc", token);
    }

    [Fact]
    public async Task StoreTokenAsync_ForExistingOwner_ReplacesToken()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());
        await vault.StoreTokenAsync("PerfectServe", "first_token", CancellationToken.None);

        // Act
        await vault.StoreTokenAsync("PerfectServe", "second_token", CancellationToken.None);

        // Assert
        Assert.Equal(
            "second_token",
            await vault.GetTokenAsync("PerfectServe", CancellationToken.None)
        );
        Assert.Equal(
            1,
            await context.OwnerTokens.AsNoTracking().CountAsync(CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreTokenAsync_DoesNotPersistPlaintext()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());
        const string plaintext = "github_pat_supersecret";

        // Act
        await vault.StoreTokenAsync("PerfectServe", plaintext, CancellationToken.None);

        // Assert
        var stored = await context.OwnerTokens.AsNoTracking().SingleAsync(CancellationToken.None);
        Assert.NotEqual(Encoding.UTF8.GetBytes(plaintext), stored.Ciphertext);
    }

    [Fact]
    public async Task GetTokenAsync_WhenNoTokenStored_ReturnsNull()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());

        // Act
        var token = await vault.GetTokenAsync("PerfectServe", CancellationToken.None);

        // Assert
        Assert.Null(token);
    }

    [Fact]
    public async Task StoreTokenAsync_WhileLocked_ThrowsVaultLocked()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, new VaultKeyHolder());

        // Act / Assert
        await Assert.ThrowsAsync<VaultLockedException>(() =>
            vault.StoreTokenAsync("PerfectServe", "github_pat_abc", CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetTokenAsync_WhileLocked_ThrowsVaultLocked()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, new VaultKeyHolder());

        // Act / Assert
        await Assert.ThrowsAsync<VaultLockedException>(() =>
            vault.GetTokenAsync("PerfectServe", CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StoreTokenAsync_NullOrWhitespaceOwner_Throws(string? owner)
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            vault.StoreTokenAsync(owner!, "github_pat_abc", CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StoreTokenAsync_NullOrWhitespaceToken_Throws(string? token)
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            vault.StoreTokenAsync("PerfectServe", token!, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTokenAsync_NullOrWhitespaceOwner_Throws(string? owner)
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            vault.GetTokenAsync(owner!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task ResetVaultAsync_DeletesTokensAndSecurityAndClearsKey()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityAndToken(context);
        var keyHolder = Unlocked();
        var vault = CreateVault(context, keyHolder);

        // Act
        await vault.ResetVaultAsync(CancellationToken.None);

        // Assert
        Assert.False(await context.AppSecurity.AsNoTracking().AnyAsync(CancellationToken.None));
        Assert.False(await context.OwnerTokens.AsNoTracking().AnyAsync(CancellationToken.None));
        Assert.False(keyHolder.HasKey);
    }

    [Fact]
    public async Task ResetVaultAsync_WhileLocked_StillWipesVault()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityAndToken(context);
        var vault = CreateVault(context, new VaultKeyHolder());

        // Act
        await vault.ResetVaultAsync(CancellationToken.None);

        // Assert
        Assert.False(await context.AppSecurity.AsNoTracking().AnyAsync(CancellationToken.None));
        Assert.False(await context.OwnerTokens.AsNoTracking().AnyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StoreTokenAsync_AfterResetInSameScope_WritesCleanly()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var keyHolder = Unlocked();
        var vault = CreateVault(context, keyHolder);
        await vault.StoreTokenAsync("PerfectServe", "first_token", CancellationToken.None);
        await vault.ResetVaultAsync(CancellationToken.None);
        keyHolder.SetKey(RandomNumberGenerator.GetBytes(32));

        // Act
        await vault.StoreTokenAsync("PerfectServe", "second_token", CancellationToken.None);

        // Assert
        Assert.Equal(
            "second_token",
            await vault.GetTokenAsync("PerfectServe", CancellationToken.None)
        );
    }

    private static TokenVault CreateVault(PrCenterDbContext context, VaultKeyHolder keyHolder) =>
        new(context, keyHolder, NullLogger<TokenVault>.Instance);

    private static VaultKeyHolder Unlocked()
    {
        var holder = new VaultKeyHolder();
        holder.SetKey(RandomNumberGenerator.GetBytes(32));
        return holder;
    }

    private static void SeedSecurityAndToken(PrCenterDbContext context)
    {
        context.AppSecurity.Add(
            new AppSecurity
            {
                Id = 1,
                Salt = [1],
                MemoryKib = 1024,
                Iterations = 1,
                Parallelism = 1,
                KdfVersion = 1,
                SentinelNonce = [2],
                SentinelCiphertext = [3],
                SentinelTag = [4],
            }
        );
        context.OwnerTokens.Add(
            new OwnerToken
            {
                Owner = "PerfectServe",
                Nonce = [1],
                Ciphertext = [2],
                Tag = [3],
            }
        );
        context.SaveChanges();
    }

    public void Dispose() => _database.Dispose();
}
