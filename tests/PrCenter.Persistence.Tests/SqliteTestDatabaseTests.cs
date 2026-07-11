using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence.Tests;

public sealed class SqliteTestDatabaseTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task MigratedDatabase_PersistsAndReadsBackAMarker()
    {
        // Arrange
        var seenAt = DateTimeOffset.Parse("2026-07-01T09:30:00Z", CultureInfo.InvariantCulture);
        await using (var write = _database.CreateContext())
        {
            write.LastSeenMarkers.Add(
                new LastSeenMarker { PullRequestId = "pr-1", SeenAt = seenAt }
            );
            await write.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var read = _database.CreateContext();
        var marker = await read.LastSeenMarkers.FindAsync(["pr-1"], CancellationToken.None);

        // Assert
        Assert.NotNull(marker);
        Assert.Equal(seenAt, marker.SeenAt);
    }

    public void Dispose() => _database.Dispose();
}
