namespace PrCenter.Core.Tests.Queue;

using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

public sealed class OwnerStatusTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceOwner_Throws(string? owner)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() => new OwnerStatus(owner!, OwnerFetchStatus.Ok));
    }

    [Fact]
    public void Constructor_WithLastFreshAt_ExposesIt()
    {
        // Arrange
        var lastFreshAt = new DateTimeOffset(2026, 7, 14, 13, 55, 0, TimeSpan.Zero);

        // Act
        var status = new OwnerStatus(
            "PerfectServe",
            OwnerFetchStatus.Error,
            detail: "boom",
            lastFreshAt: lastFreshAt
        );

        // Assert
        Assert.Equal(lastFreshAt, status.LastFreshAt);
    }

    [Fact]
    public void Constructor_WithoutLastFreshAt_IsNull()
    {
        // Act
        var status = new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok);

        // Assert
        Assert.Null(status.LastFreshAt);
    }
}
