using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Core.Tests.Diagnostics;

public sealed class OwnerPollDiagnosticsTests
{
    [Theory]
    [InlineData("window")]
    [InlineData("outcome")]
    [InlineData("contributed")]
    public void Constructor_WithNullRequiredPart_Throws(string missingPart)
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Create(missingPart));
    }

    [Fact]
    public void Constructor_WithoutCountsOrExclusionsOrRateLimit_LeavesThemAbsent()
    {
        // Act
        var row = new OwnerPollDiagnostics(
            new OwnerPollWindow("acme"),
            new OwnerPollOutcome(OwnerFetchStatus.NotPolled),
            counts: null,
            exclusions: null,
            rateLimit: null,
            ContributedPullRequests.None
        );

        // Assert
        Assert.Null(row.Counts);
        Assert.Null(row.Exclusions);
        Assert.Null(row.RateLimit);
    }

    private static OwnerPollDiagnostics Create(string missingPart) =>
        new(
            missingPart == "window" ? null! : new OwnerPollWindow("acme"),
            missingPart == "outcome" ? null! : new OwnerPollOutcome(OwnerFetchStatus.Ok),
            FetchCounts.Fetched(1, 0, 1, 1),
            new ExclusionCounts(0, 0, 0, 0),
            rateLimit: null,
            missingPart == "contributed" ? null! : ContributedPullRequests.None
        );
}
