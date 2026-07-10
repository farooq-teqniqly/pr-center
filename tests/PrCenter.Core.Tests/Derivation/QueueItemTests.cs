using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class QueueItemTests
{
    [Fact]
    public void Constructor_WithNullIdentity_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new QueueItem(
                identity: null!,
                lastUpdatedBy: TestLogins.Author,
                lastUpdatedAt: TestTime.At(1),
                state: MembershipState.AwaitingFirstReview,
                hasUpdate: false,
                isAlreadyCovered: false
            )
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithMissingLastUpdatedBy_Throws(string? lastUpdatedBy)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            new QueueItem(
                identity: Identity(),
                lastUpdatedBy: lastUpdatedBy!,
                lastUpdatedAt: TestTime.At(1),
                state: MembershipState.AwaitingFirstReview,
                hasUpdate: false,
                isAlreadyCovered: false
            )
        );
    }

    private static PullRequestIdentity Identity() =>
        new(
            id: "owner/repo#1",
            owner: "owner",
            repository: "repo",
            number: 1,
            title: "Add feature",
            url: "https://github.com/owner/repo/pull/1"
        );
}
