using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PrCenter.Core.Locking;
using PrCenter.Persistence;

namespace PrCenter.Persistence.Tests;

public sealed class AppLockTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task GetStateAsync_WhenNoSecurityRow_ReturnsUninitialized()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, new VaultKeyHolder());

        // Act
        var state = await appLock.GetStateAsync(CancellationToken.None);

        // Assert
        Assert.Equal(AppLockState.Uninitialized, state);
    }

    [Fact]
    public async Task GetStateAsync_WhenSecurityRowExistsAndNoKeyHeld_ReturnsLocked()
    {
        // Arrange
        await SetPasswordAsync("pw");
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, new VaultKeyHolder());

        // Act
        var state = await appLock.GetStateAsync(CancellationToken.None);

        // Assert
        Assert.Equal(AppLockState.Locked, state);
    }

    [Fact]
    public async Task GetStateAsync_WhenSecurityRowExistsAndKeyHeld_ReturnsUnlocked()
    {
        // Arrange
        await SetPasswordAsync("pw");
        var keyHolder = new VaultKeyHolder();
        keyHolder.SetKey([1, 2, 3]);
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, keyHolder);

        // Act
        var state = await appLock.GetStateAsync(CancellationToken.None);

        // Assert
        Assert.Equal(AppLockState.Unlocked, state);
    }

    [Fact]
    public async Task UnlockAsync_WithCorrectPassword_UnlocksAndHoldsKey()
    {
        // Arrange
        await SetPasswordAsync("correct horse");
        var keyHolder = new VaultKeyHolder();
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, keyHolder);

        // Act
        var unlocked = await appLock.UnlockAsync("correct horse", CancellationToken.None);

        // Assert
        Assert.True(unlocked);
        Assert.True(keyHolder.HasKey);
        Assert.Equal(AppLockState.Unlocked, await appLock.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnlockAsync_WithWrongPassword_ReturnsFalseAndStaysLocked()
    {
        // Arrange
        await SetPasswordAsync("correct horse");
        var keyHolder = new VaultKeyHolder();
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, keyHolder);

        // Act
        var unlocked = await appLock.UnlockAsync("wrong password", CancellationToken.None);

        // Assert
        Assert.False(unlocked);
        Assert.False(keyHolder.HasKey);
        Assert.Equal(AppLockState.Locked, await appLock.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnlockAsync_WhenAlreadyUnlocked_IsIdempotentAndDoesNotRekey()
    {
        // Arrange
        await SetPasswordAsync("correct horse");
        var keyHolder = new VaultKeyHolder();
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, keyHolder);
        Assert.True(await appLock.UnlockAsync("correct horse", CancellationToken.None));
        var keyAfterFirstUnlock = keyHolder.GetKeyOrThrow();

        // Act
        var secondResult = await appLock.UnlockAsync("any password", CancellationToken.None);

        // Assert
        Assert.True(secondResult);
        Assert.Equal(keyAfterFirstUnlock, keyHolder.GetKeyOrThrow());
        Assert.Equal(AppLockState.Unlocked, await appLock.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UnlockAsync_WhenKdfVersionUnsupported_Throws()
    {
        // Arrange
        await using var context = _database.CreateContext();
        context.AppSecurity.Add(
            new AppSecurity
            {
                Id = 1,
                Salt = [1],
                MemoryKib = 1024,
                Iterations = 1,
                Parallelism = 1,
                KdfVersion = AppSecurity.SupportedKdfVersion + 1,
                SentinelNonce = [2],
                SentinelCiphertext = [3],
                SentinelTag = [4],
            }
        );
        await context.SaveChangesAsync(CancellationToken.None);
        var appLock = CreateAppLock(context, new VaultKeyHolder());

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            appLock.UnlockAsync("correct horse", CancellationToken.None)
        );
    }

    [Fact]
    public async Task UnlockAsync_WhenUninitialized_Throws()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, new VaultKeyHolder());

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            appLock.UnlockAsync("pw", CancellationToken.None)
        );
    }

    [Fact]
    public async Task UnlockAsync_ImmediatelyAfterSetPassword_VerifiesWithNoTokensStored()
    {
        // Arrange
        await SetPasswordAsync("correct horse");
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, new VaultKeyHolder());

        // Act
        var unlocked = await appLock.UnlockAsync("correct horse", CancellationToken.None);

        // Assert
        Assert.True(unlocked);
        Assert.Empty(await context.OwnerTokens.AsNoTracking().ToListAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnlockAsync_NullOrWhitespacePassword_Throws(string? password)
    {
        // Arrange
        await using var context = _database.CreateContext();
        var appLock = CreateAppLock(context, new VaultKeyHolder());

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            appLock.UnlockAsync(password!, CancellationToken.None)
        );
    }

    private static AppLock CreateAppLock(PrCenterDbContext context, VaultKeyHolder keyHolder) =>
        new(context, keyHolder, NullLogger<AppLock>.Instance);

    private async Task SetPasswordAsync(string password)
    {
        await using var context = _database.CreateContext();
        var vault = new TokenVault(context, new VaultKeyHolder(), NullLogger<TokenVault>.Instance);
        await vault.SetPasswordAsync(password, CancellationToken.None);
    }

    public void Dispose() => _database.Dispose();
}
