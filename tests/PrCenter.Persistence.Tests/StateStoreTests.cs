using PrCenter.Persistence;

namespace PrCenter.Persistence.Tests;

public sealed class StateStoreTests
{
    [Fact]
    public async Task GetLastSeenAsync_WhenCalled_ThrowsNotImplemented()
    {
        // Arrange
        var store = new StateStore();

        // Act / Assert
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            store.GetLastSeenAsync("pr-1", CancellationToken.None)
        );
    }

    [Fact]
    public async Task SetLastSeenAsync_WhenCalled_ThrowsNotImplemented()
    {
        // Arrange
        var store = new StateStore();

        // Act / Assert
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            store.SetLastSeenAsync("pr-1", DateTimeOffset.UtcNow, CancellationToken.None)
        );
    }
}
