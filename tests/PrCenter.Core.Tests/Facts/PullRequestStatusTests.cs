using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Facts;

public sealed class PullRequestStatusTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithMissingLastUpdatedBy_Throws(string? lastUpdatedBy)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            new PullRequestStatus(
                isDraft: false,
                isClosedOrMerged: false,
                lastUpdatedBy: lastUpdatedBy!,
                lastUpdatedAt: DateTimeOffset.UtcNow
            )
        );
    }
}
