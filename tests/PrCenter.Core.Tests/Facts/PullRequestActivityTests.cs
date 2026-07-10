using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Facts;

public sealed class PullRequestActivityTests
{
    [Theory]
    [InlineData("requestedReviewerLogins")]
    [InlineData("reviews")]
    [InlineData("commits")]
    [InlineData("comments")]
    public void Constructor_WithNullCollection_Throws(string nullArgument)
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ConstructWithNull(nullArgument));
    }

    [Theory]
    [InlineData("requestedReviewerLogins")]
    [InlineData("reviews")]
    [InlineData("commits")]
    [InlineData("comments")]
    public void Constructor_WithNullElement_Throws(string collectionWithNull)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => ConstructWithNullElement(collectionWithNull));
    }

    [Fact]
    public void Constructor_DoesNotObserveLaterMutationOfSourceCollections()
    {
        // Arrange
        var logins = new List<string> { "octocat" };
        var activity = new PullRequestActivity(logins, [], [], []);

        // Act
        logins.Add("hubot");

        // Assert
        Assert.Single(activity.RequestedReviewerLogins);
    }

    private static PullRequestActivity ConstructWithNull(string nullArgument) =>
        new(
            requestedReviewerLogins: nullArgument == "requestedReviewerLogins" ? null! : [],
            reviews: nullArgument == "reviews" ? null! : [],
            commits: nullArgument == "commits" ? null! : [],
            comments: nullArgument == "comments" ? null! : []
        );

    private static PullRequestActivity ConstructWithNullElement(string collectionWithNull) =>
        new(
            requestedReviewerLogins: collectionWithNull == "requestedReviewerLogins" ? [null!] : [],
            reviews: collectionWithNull == "reviews" ? [null!] : [],
            commits: collectionWithNull == "commits" ? [null!] : [],
            comments: collectionWithNull == "comments" ? [null!] : []
        );
}
