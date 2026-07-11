using PrCenter.Core.Facts;
using PrCenter.Core.Ports;

namespace PrCenter.Core.Tests.Ports;

public sealed class OwnerFactsResultTests
{
    [Fact]
    public void Constructor_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OwnerFactsResult(OwnerFetchStatus.Ok, null!)
        );
    }

    [Fact]
    public void Constructor_WithEmptyFacts_IsOkWithNoFacts()
    {
        // Act
        var result = new OwnerFactsResult(OwnerFetchStatus.Ok, []);

        // Assert
        Assert.Equal(OwnerFetchStatus.Ok, result.Status);
        Assert.Empty(result.Facts);
        Assert.Null(result.Detail);
    }

    [Fact]
    public void Constructor_WithStatusFactsAndDetail_ExposesThem()
    {
        // Arrange
        var facts = new[] { SampleFacts() };

        // Act
        var result = new OwnerFactsResult(
            OwnerFetchStatus.MisconfiguredToken,
            [],
            detail: "token rejected"
        );
        var okResult = new OwnerFactsResult(OwnerFetchStatus.Ok, facts);

        // Assert
        Assert.Equal(OwnerFetchStatus.MisconfiguredToken, result.Status);
        Assert.Equal("token rejected", result.Detail);
        Assert.Single(okResult.Facts);
    }

    [Fact]
    public void Constructor_DoesNotObserveLaterMutationOfSourceList()
    {
        // Arrange
        var facts = new List<PullRequestFacts> { SampleFacts() };
        var result = new OwnerFactsResult(OwnerFetchStatus.Ok, facts);

        // Act
        facts.Add(SampleFacts());

        // Assert
        Assert.Single(result.Facts);
    }

    private static PullRequestFacts SampleFacts() =>
        new(
            new PullRequestIdentity(
                id: "owner/repo#1",
                owner: "owner",
                repository: "repo",
                number: 1,
                title: "Add feature",
                url: "https://github.com/owner/repo/pull/1"
            ),
            new PullRequestStatus(
                isDraft: false,
                isClosedOrMerged: false,
                lastUpdatedBy: "octocat",
                lastUpdatedAt: DateTimeOffset.UtcNow
            ),
            new PullRequestActivity([], [], [], [])
        );
}
