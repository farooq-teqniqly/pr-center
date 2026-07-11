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
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithNullOrBlankLogin_Throws(string? login)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() => new PullRequestActivity([login!], [], [], []));
    }

    [Fact]
    public void Constructor_DoesNotObserveLaterMutationOfSourceCollections()
    {
        // Arrange
        var logins = new List<string> { TestLogins.Me };
        var activity = new PullRequestActivity(logins, [], [], []);

        // Act
        logins.Add(TestLogins.Other);

        // Assert
        Assert.Single(activity.RequestedReviewerLogins);
    }

    [Fact]
    public void Constructor_ExposesCollectionsThatCannotBeCastToAMutableArray()
    {
        // Arrange
        var activity = new PullRequestActivity([TestLogins.Me], [], [], []);

        // Act / Assert
        Assert.Null(activity.RequestedReviewerLogins as string[]);
        Assert.Null(activity.Reviews as ReviewFact[]);
        Assert.Null(activity.Commits as CommitFact[]);
        Assert.Null(activity.Comments as CommentFact[]);
    }

    private static PullRequestActivity ConstructWithNull(string nullArgument) =>
        new(
            requestedReviewerLogins: nullArgument == "requestedReviewerLogins" ? null! : [],
            reviews: nullArgument == "reviews" ? null! : [],
            commits: nullArgument == "commits" ? null! : [],
            comments: nullArgument == "comments" ? null! : []
        );
}
