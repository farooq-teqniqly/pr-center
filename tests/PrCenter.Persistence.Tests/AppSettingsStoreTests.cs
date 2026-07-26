using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PrCenter.Core.Settings;

namespace PrCenter.Persistence.Tests;

public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task GetPollIntervalAsync_NoStoredRow_ReturnsTheDefault()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        // Act
        var interval = await store.GetPollIntervalAsync(CancellationToken.None);

        // Assert
        Assert.Equal(PollInterval.Default, interval);
    }

    [Fact]
    public async Task GetPollIntervalAsync_NoStoredRow_DoesNotCreateARow()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        // Act
        await store.GetPollIntervalAsync(CancellationToken.None);

        // Assert
        var rows = await context.AppSettings.AsNoTracking().CountAsync(CancellationToken.None);
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task SetPollIntervalAsync_InRangeValue_RoundTripsThroughARealFile()
    {
        // Arrange
        await using var writeContext = _database.CreateContext();
        var interval = new PollInterval(TimeSpan.FromMinutes(37));

        // Act
        await CreateStore(writeContext).SetPollIntervalAsync(interval, CancellationToken.None);

        // Assert
        await using var readContext = _database.CreateContext();
        var stored = await CreateStore(readContext).GetPollIntervalAsync(CancellationToken.None);
        Assert.Equal(interval, stored);
    }

    [Fact]
    public async Task SetPollIntervalAsync_TheStructDefault_ThrowsAndStoresNothing()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var store = CreateStore(context);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            store.SetPollIntervalAsync(default, CancellationToken.None)
        );

        await using var readContext = _database.CreateContext();
        Assert.Equal(
            0,
            await readContext
                .AppSettings.AsNoTracking()
                .CountAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task SetPollIntervalAsync_CalledTwice_ReplacesRatherThanInserting()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var store = CreateStore(context);
        await store.SetPollIntervalAsync(
            new PollInterval(TimeSpan.FromMinutes(10)),
            CancellationToken.None
        );

        // Act
        await store.SetPollIntervalAsync(
            new PollInterval(TimeSpan.FromHours(2)),
            CancellationToken.None
        );

        // Assert
        await using var readContext = _database.CreateContext();
        var rows = await readContext.AppSettings.AsNoTracking().ToListAsync(CancellationToken.None);
        var only = Assert.Single(rows);
        Assert.Equal(TimeSpan.FromHours(2).TotalSeconds, only.PollIntervalSeconds);
    }

    [Theory]
    [InlineData(1, 5 * 60)]
    [InlineData(0, 5 * 60)]
    [InlineData(-30, 5 * 60)]
    [InlineData(48 * 60 * 60, 24 * 60 * 60)]
    [InlineData(long.MaxValue, 24 * 60 * 60)]
    [InlineData(long.MinValue, 5 * 60)]
    public async Task GetPollIntervalAsync_OutOfRangeStoredValue_ReturnsTheClampedValue(
        long storedSeconds,
        long expectedSeconds
    )
    {
        // Arrange
        await using var seedContext = _database.CreateContext();
        seedContext.AppSettings.Add(
            new AppSetting { Id = AppSetting.SingletonId, PollIntervalSeconds = storedSeconds }
        );
        await seedContext.SaveChangesAsync(CancellationToken.None);
        await using var context = _database.CreateContext();

        // Act
        var interval = await CreateStore(context).GetPollIntervalAsync(CancellationToken.None);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), interval.Value);
    }

    [Fact]
    public async Task GetPollIntervalAsync_OutOfRangeStoredValue_LogsAWarning()
    {
        // Arrange
        await using var seedContext = _database.CreateContext();
        seedContext.AppSettings.Add(
            new AppSetting { Id = AppSetting.SingletonId, PollIntervalSeconds = 1 }
        );
        await seedContext.SaveChangesAsync(CancellationToken.None);
        await using var context = _database.CreateContext();
        var logger = new CapturingLogger<AppSettingsStore>();

        // Act
        await new AppSettingsStore(context, logger).GetPollIntervalAsync(CancellationToken.None);

        // Assert
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task GetPollIntervalAsync_InRangeStoredValue_LogsNoWarning()
    {
        // Arrange
        await using var context = _database.CreateContext();
        var logger = new CapturingLogger<AppSettingsStore>();
        var store = new AppSettingsStore(context, logger);
        await store.SetPollIntervalAsync(
            new PollInterval(TimeSpan.FromMinutes(15)),
            CancellationToken.None
        );

        // Act
        await store.GetPollIntervalAsync(CancellationToken.None);

        // Assert
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task GetPollIntervalAsync_WhileVaultIsLocked_Succeeds()
    {
        // Arrange
        await using var writeContext = _database.CreateContext();
        var interval = new PollInterval(TimeSpan.FromMinutes(45));
        await CreateStore(writeContext).SetPollIntervalAsync(interval, CancellationToken.None);
        await using var context = _database.CreateContext();

        // Act
        var stored = await CreateStore(context).GetPollIntervalAsync(CancellationToken.None);

        // Assert
        Assert.Equal(interval, stored);
    }

    public void Dispose() => _database.Dispose();

    private static AppSettingsStore CreateStore(PrCenterDbContext context) =>
        new(context, NullLogger<AppSettingsStore>.Instance);
}
