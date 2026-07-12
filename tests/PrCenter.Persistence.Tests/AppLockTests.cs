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
        var appLock = new AppLock(context, new VaultKeyHolder());

        // Act
        var state = await appLock.GetStateAsync(CancellationToken.None);

        // Assert
        Assert.Equal(AppLockState.Uninitialized, state);
    }

    [Fact]
    public async Task GetStateAsync_WhenSecurityRowExistsAndNoKeyHeld_ReturnsLocked()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityRow(context);
        var appLock = new AppLock(context, new VaultKeyHolder());

        // Act
        var state = await appLock.GetStateAsync(CancellationToken.None);

        // Assert
        Assert.Equal(AppLockState.Locked, state);
    }

    [Fact]
    public async Task GetStateAsync_WhenSecurityRowExistsAndKeyHeld_ReturnsUnlocked()
    {
        // Arrange
        await using var context = _database.CreateContext();
        SeedSecurityRow(context);
        var keyHolder = new VaultKeyHolder();
        keyHolder.SetKey([1, 2, 3]);
        var appLock = new AppLock(context, keyHolder);

        // Act
        var state = await appLock.GetStateAsync(CancellationToken.None);

        // Assert
        Assert.Equal(AppLockState.Unlocked, state);
    }

    private static void SeedSecurityRow(PrCenterDbContext context)
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
        context.SaveChanges();
    }

    public void Dispose() => _database.Dispose();
}
