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
}
