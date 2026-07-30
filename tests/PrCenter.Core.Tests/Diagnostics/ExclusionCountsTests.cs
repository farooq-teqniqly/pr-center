using PrCenter.Core.Diagnostics;

namespace PrCenter.Core.Tests.Diagnostics;

public sealed class ExclusionCountsTests
{
    [Theory]
    [InlineData(6, 2, 3, 0, 11)]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(1, 1, 1, 1, 4)]
    public void Total_SumsEveryExclusionReason(
        int draft,
        int closedOrMerged,
        int approved,
        int untracked,
        int expected
    )
    {
        // Act
        var counts = new ExclusionCounts(draft, closedOrMerged, approved, untracked);

        // Assert
        Assert.Equal(expected, counts.Total);
    }
}
