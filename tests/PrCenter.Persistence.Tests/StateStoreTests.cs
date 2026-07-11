using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence.Tests;

public sealed class StateStoreTests : IDisposable
{
    private static readonly DateTimeOffset First = DateTimeOffset.Parse(
        "2026-07-01T09:00:00Z",
        CultureInfo.InvariantCulture
    );
    private static readonly DateTimeOffset Second = DateTimeOffset.Parse(
        "2026-07-02T10:00:00Z",
        CultureInfo.InvariantCulture
    );

    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task SetLastSeenAsync_ThenGetLastSeenAsync_ReturnsTheInstant()
    {
        // Arrange
        await using (var write = _database.CreateContext())
        {
            await new StateStore(write).SetLastSeenAsync("pr-1", First, CancellationToken.None);
        }

        // Act
        await using var read = _database.CreateContext();
        var seenAt = await new StateStore(read).GetLastSeenAsync("pr-1", CancellationToken.None);

        // Assert
        Assert.Equal(First, seenAt);
    }

    [Fact]
    public async Task GetLastSeenAsync_WhenNoMarker_ReturnsNull()
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Act
        var seenAt = await new StateStore(context).GetLastSeenAsync(
            "never-seen",
            CancellationToken.None
        );

        // Assert
        Assert.Null(seenAt);
    }

    [Fact]
    public async Task SetLastSeenAsync_WhenCalledTwice_UpdatesTheSameMarker()
    {
        // Arrange
        await using (var write = _database.CreateContext())
        {
            var store = new StateStore(write);
            await store.SetLastSeenAsync("pr-1", First, CancellationToken.None);
            await store.SetLastSeenAsync("pr-1", Second, CancellationToken.None);
        }

        // Act
        await using var read = _database.CreateContext();
        var seenAt = await new StateStore(read).GetLastSeenAsync("pr-1", CancellationToken.None);
        var rowCount = await read.LastSeenMarkers.CountAsync(CancellationToken.None);

        // Assert
        Assert.Equal(Second, seenAt);
        Assert.Equal(1, rowCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetLastSeenAsync_WithMissingId_Throws(string? pullRequestId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            new StateStore(context).GetLastSeenAsync(pullRequestId!, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SetLastSeenAsync_WithMissingId_Throws(string? pullRequestId)
    {
        // Arrange
        await using var context = _database.CreateContext();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            new StateStore(context).SetLastSeenAsync(pullRequestId!, First, CancellationToken.None)
        );
    }

    public void Dispose() => _database.Dispose();
}
