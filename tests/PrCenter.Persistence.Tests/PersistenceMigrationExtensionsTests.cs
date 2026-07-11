using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence.Tests;

public sealed class PersistenceMigrationExtensionsTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"prcenter-migrate-{Guid.NewGuid():N}.db"
    );

    [Fact]
    public async Task MigratePersistenceAsync_SchemaLessFile_CreatesMarkerTableForRoundTrip()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPersistenceAdapter($"Data Source={_path}", isDevelopment: false);
        await using var provider = services.BuildServiceProvider();
        var seenAt = DateTimeOffset.Parse(
            "2026-07-01T09:30:00Z",
            System.Globalization.CultureInfo.InvariantCulture
        );

        // Act
        await provider.MigratePersistenceAsync(CancellationToken.None);

        // Assert
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();
        await store.SetLastSeenAsync("pr-1", seenAt, CancellationToken.None);
        var read = await store.GetLastSeenAsync("pr-1", CancellationToken.None);
        Assert.Equal(seenAt, read);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in (ReadOnlySpan<string>)["", "-wal", "-shm"])
        {
            var file = _path + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
