using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Facts;

public sealed class PullRequestFactsTests
{
    [Theory]
    [InlineData("identity")]
    [InlineData("status")]
    [InlineData("activity")]
    public void Constructor_WithNullSubRecord_Throws(string nullArgument)
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ConstructWithNull(nullArgument));
    }

    [Fact]
    public void Constructor_WithValidArguments_ExposesSubRecords()
    {
        // Arrange
        var identity = ValidIdentity();
        var status = ValidStatus();
        var activity = ValidActivity();

        // Act
        var facts = new PullRequestFacts(identity, status, activity);

        // Assert
        Assert.Same(identity, facts.Identity);
        Assert.Same(status, facts.Status);
        Assert.Same(activity, facts.Activity);
    }

    private static PullRequestFacts ConstructWithNull(string nullArgument)
    {
        var identity = nullArgument == "identity" ? null : ValidIdentity();
        var status = nullArgument == "status" ? null : ValidStatus();
        var activity = nullArgument == "activity" ? null : ValidActivity();

        return new PullRequestFacts(identity!, status!, activity!);
    }

    private static PullRequestIdentity ValidIdentity() =>
        new(
            id: "owner/repo#1",
            owner: "owner",
            repository: "repo",
            number: 1,
            title: "Add feature",
            url: "https://github.com/owner/repo/pull/1"
        );

    private static PullRequestStatus ValidStatus() =>
        new(
            isDraft: false,
            isClosedOrMerged: false,
            lastUpdatedBy: "octocat",
            lastUpdatedAt: DateTimeOffset.UtcNow
        );

    private static PullRequestActivity ValidActivity() =>
        new(requestedReviewerLogins: [], reviews: [], commits: [], comments: []);
}
