using PrCenter.Core.Diagnostics;

namespace PrCenter.Core.Tests.Diagnostics;

public sealed class FetchCountsTests
{
    [Fact]
    public void Fetched_CarriesTheFetchNumbersAndNothingCarriedOver()
    {
        // Act
        var counts = FetchCounts.Fetched(requested: 12, reviewed: 8, union: 15, derived: 4);

        // Assert
        Assert.Equal(12, counts.Requested);
        Assert.Equal(8, counts.Reviewed);
        Assert.Equal(15, counts.Union);
        Assert.Equal(4, counts.Derived);
        Assert.Equal(0, counts.CarriedOver);
    }

    [Fact]
    public void NothingFetched_LeavesEveryFetchNumberAbsent()
    {
        // Act
        var counts = FetchCounts.NothingFetched(carriedOver: 5);

        // Assert -- absent, not zero: no search ran to return nothing
        Assert.Null(counts.Requested);
        Assert.Null(counts.Reviewed);
        Assert.Null(counts.Union);
        Assert.Null(counts.Derived);
        Assert.Equal(5, counts.CarriedOver);
    }

    [Fact]
    public void NothingFetched_WithNothingToCarry_StillReportsZeroRatherThanAbsent()
    {
        // Act -- an owner that has never been fresh carries zero rows
        var counts = FetchCounts.NothingFetched(carriedOver: 0);

        // Assert
        Assert.Equal(0, counts.CarriedOver);
    }
}
