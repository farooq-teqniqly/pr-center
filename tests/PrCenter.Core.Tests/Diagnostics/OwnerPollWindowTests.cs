using PrCenter.Core.Diagnostics;

namespace PrCenter.Core.Tests.Diagnostics;

public sealed class OwnerPollWindowTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithMissingOwner_Throws(string? owner)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() => new OwnerPollWindow(owner!));
    }

    [Fact]
    public void Constructor_WithoutInstants_LeavesThemAbsent()
    {
        // Act
        var window = new OwnerPollWindow("acme");

        // Assert
        Assert.Null(window.StartedAt);
        Assert.Null(window.CompletedAt);
    }

    [Fact]
    public void Constructor_WithInstants_ExposesThem()
    {
        // Arrange
        var startedAt = new DateTimeOffset(2026, 7, 29, 14, 5, 0, TimeSpan.Zero);

        // Act
        var window = new OwnerPollWindow("acme", startedAt, startedAt.AddSeconds(3));

        // Assert
        Assert.Equal(startedAt, window.StartedAt);
        Assert.Equal(startedAt.AddSeconds(3), window.CompletedAt);
    }
}
