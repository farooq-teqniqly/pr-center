using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PrCenter.Core.Locking;
using PrCenter.Persistence;

namespace PrCenter.Persistence.Tests;

public sealed class TokenVaultTests : IDisposable
{
    private static readonly DateTimeOffset StoredAt = new(2026, 7, 26, 9, 30, 0, TimeSpan.Zero);

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

    [Fact]
    public async Task GetTokenAsync_WhenStoredRowCannotBeDecrypted_ThrowsInvalidOperation()
    {
        // Arrange
        await using var context = _database.CreateContext();
        context.OwnerTokens.Add(
            new OwnerToken
            {
                Owner = "PerfectServe",
                Nonce = new byte[12],
                Ciphertext = [1, 2, 3],
                Tag = new byte[16],
            }
        );
        await context.SaveChangesAsync(CancellationToken.None);
        var vault = CreateVault(context, Unlocked());

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            vault.GetTokenAsync("PerfectServe", CancellationToken.None)
        );
    }

    [Fact]
    public async Task ListOwnersAsync_WithStoredTokens_ReturnsThoseOwners()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());
        await vault.StoreTokenAsync("PerfectServe", "token_a", CancellationToken.None);
        await vault.StoreTokenAsync("ps-unite", "token_b", CancellationToken.None);

        // Act
        var owners = await vault.ListOwnersAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["PerfectServe", "ps-unite"], owners.OrderBy(owner => owner));
    }

    [Fact]
    public async Task ListOwnersAsync_WithEmptyVault_ReturnsEmptyList()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());

        // Act
        var owners = await vault.ListOwnersAsync(CancellationToken.None);

        // Assert
        Assert.Empty(owners);
    }

    [Fact]
    public async Task ListOwnersAsync_WhileLocked_ReturnsOwnersWithoutDecrypting()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityAndToken(context);
        var keyHolder = new VaultKeyHolder();
        var vault = CreateVault(context, keyHolder);

        // Act
        var owners = await vault.ListOwnersAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["PerfectServe"], owners);
        Assert.False(keyHolder.HasKey);
    }

    [Fact]
    public async Task DeleteTokenAsync_OwnerWithToken_RemovesItFromTheOwnerList()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());
        await vault.StoreTokenAsync("PerfectServe", "github_pat_abc", CancellationToken.None);

        // Act
        await vault.DeleteTokenAsync("PerfectServe", CancellationToken.None);

        // Assert
        var owners = await vault.ListOwnersAsync(CancellationToken.None);
        Assert.Empty(owners);
    }

    [Fact]
    public async Task DeleteTokenAsync_OneOfSeveralOwners_LeavesTheOthersIntact()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());
        await vault.StoreTokenAsync("PerfectServe", "github_pat_abc", CancellationToken.None);
        await vault.StoreTokenAsync("ps-unite", "github_pat_def", CancellationToken.None);

        // Act
        await vault.DeleteTokenAsync("PerfectServe", CancellationToken.None);

        // Assert
        var owners = await vault.ListOwnersAsync(CancellationToken.None);
        Assert.Equal(["ps-unite"], owners);
    }

    [Fact]
    public async Task DeleteTokenAsync_OwnerWithToken_LeavesTheSecurityRowIntact()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());
        SeedSecurityAndToken(context);

        // Act
        await vault.DeleteTokenAsync("PerfectServe", CancellationToken.None);

        // Assert
        await using var readContext = _database.CreateContext();
        Assert.True(await readContext.AppSecurity.AsNoTracking().AnyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeleteTokenAsync_UnknownOwner_SucceedsAndRemovesNothing()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());
        await vault.StoreTokenAsync("PerfectServe", "github_pat_abc", CancellationToken.None);

        // Act
        await vault.DeleteTokenAsync("never-stored", CancellationToken.None);

        // Assert
        var owners = await vault.ListOwnersAsync(CancellationToken.None);
        Assert.Equal(["PerfectServe"], owners);
    }

    [Fact]
    public async Task DeleteTokenAsync_WhileLocked_ThrowsVaultLockedAndRemovesNothing()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityAndToken(context);
        var vault = CreateVault(context, new VaultKeyHolder());

        // Act
        await Assert.ThrowsAsync<VaultLockedException>(() =>
            vault.DeleteTokenAsync("PerfectServe", CancellationToken.None)
        );

        // Assert
        await using var readContext = _database.CreateContext();
        Assert.Equal(
            1,
            await readContext.OwnerTokens.AsNoTracking().CountAsync(CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteTokenAsync_NullOrWhitespaceOwner_Throws(string? owner)
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            vault.DeleteTokenAsync(owner!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreTokenAsync_NewOwner_RecordsTheSavedInstant()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var clock = new FakeTimeProvider(StoredAt);
        var vault = CreateVault(context, Unlocked(), clock);

        // Act
        await vault.StoreTokenAsync("PerfectServe", "github_pat_abc", CancellationToken.None);

        // Assert
        var summaries = await vault.ListOwnerTokensAsync(CancellationToken.None);
        Assert.Equal(StoredAt, Assert.Single(summaries).SavedAt);
    }

    [Fact]
    public async Task StoreTokenAsync_ReplacingAToken_UpdatesTheSavedInstant()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var clock = new FakeTimeProvider(StoredAt);
        var vault = CreateVault(context, Unlocked(), clock);
        await vault.StoreTokenAsync("PerfectServe", "github_pat_abc", CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(3));

        // Act
        await vault.StoreTokenAsync("PerfectServe", "github_pat_def", CancellationToken.None);

        // Assert
        var summaries = await vault.ListOwnerTokensAsync(CancellationToken.None);
        Assert.Equal(StoredAt.AddDays(3), Assert.Single(summaries).SavedAt);
    }

    [Fact]
    public async Task ListOwnerTokensAsync_RowWrittenWithoutAnInstant_ReportsNoSavedInstant()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityAndToken(context);
        var vault = CreateVault(context, Unlocked());

        // Act
        var summaries = await vault.ListOwnerTokensAsync(CancellationToken.None);

        // Assert
        Assert.Null(Assert.Single(summaries).SavedAt);
    }

    [Fact]
    public async Task ListOwnerTokensAsync_SeveralOwners_ReturnsOneSummaryPerOwner()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());
        await vault.StoreTokenAsync("PerfectServe", "github_pat_abc", CancellationToken.None);
        await vault.StoreTokenAsync("ps-unite", "github_pat_def", CancellationToken.None);

        // Act
        var summaries = await vault.ListOwnerTokensAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["PerfectServe", "ps-unite"], summaries.Select(s => s.Owner).Order());
    }

    [Fact]
    public async Task ListOwnerTokensAsync_WhileLocked_ReturnsSummariesWithoutDecrypting()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityAndToken(context);
        var keyHolder = new VaultKeyHolder();
        var vault = CreateVault(context, keyHolder);

        // Act
        var summaries = await vault.ListOwnerTokensAsync(CancellationToken.None);

        // Assert
        Assert.Equal("PerfectServe", Assert.Single(summaries).Owner);
        Assert.False(keyHolder.HasKey);
    }

    [Fact]
    public async Task ListOwnerTokensAsync_NoStoredTokens_ReturnsEmpty()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var vault = CreateVault(context, Unlocked());

        // Act
        var summaries = await vault.ListOwnerTokensAsync(CancellationToken.None);

        // Assert
        Assert.Empty(summaries);
    }

    [Fact]
    public async Task ResetVaultAsync_WithAnUnrelatedPendingChange_LeavesItTracked()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityAndToken(context);
        var vault = CreateVault(context, Unlocked());
        context.AppSettings.Add(
            new AppSetting { Id = AppSetting.SingletonId, PollIntervalSeconds = 900 }
        );

        // Act
        await vault.ResetVaultAsync(CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        // Assert
        await using var readContext = _database.CreateContext();
        var setting = await readContext
            .AppSettings.AsNoTracking()
            .SingleAsync(CancellationToken.None);
        Assert.Equal(900, setting.PollIntervalSeconds);
    }

    private static TokenVault CreateVault(
        PrCenterDbContext context,
        VaultKeyHolder keyHolder,
        TimeProvider? timeProvider = null
    ) =>
        new(
            context,
            keyHolder,
            timeProvider ?? new FakeTimeProvider(StoredAt),
            NullLogger<TokenVault>.Instance
        );

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
