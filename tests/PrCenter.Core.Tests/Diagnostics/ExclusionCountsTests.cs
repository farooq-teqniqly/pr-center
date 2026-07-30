using PrCenter.Core.Derivation;
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

    [Fact]
    public void Tally_WithNullExclusions_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ExclusionCounts.Tally(null!));
    }

    [Fact]
    public void Tally_CountsEachReasonSeparately()
    {
        // Act
        var counts = ExclusionCounts.Tally([
            MembershipExclusion.Draft,
            MembershipExclusion.Draft,
            MembershipExclusion.ClosedOrMerged,
            MembershipExclusion.Approved,
            MembershipExclusion.Untracked,
        ]);

        // Assert
        Assert.Equal(new ExclusionCounts(2, 1, 1, 1), counts);
    }

    [Fact]
    public void Tally_WithNoExclusions_CountsZeroForEveryReason()
    {
        // Act
        var counts = ExclusionCounts.Tally([]);

        // Assert
        Assert.Equal(new ExclusionCounts(0, 0, 0, 0), counts);
    }
}
