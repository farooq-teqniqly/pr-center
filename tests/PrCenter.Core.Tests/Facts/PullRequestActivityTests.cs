using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Facts;

public sealed class PullRequestActivityTests
{
    [Theory]
    [InlineData("requestedReviewerLogins")]
    [InlineData("reviews")]
    [InlineData("commits")]
    [InlineData("comments")]
    public void Constructor_WithMissingRequiredArgument_Throws(string nullArgument)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() => ConstructWithNull(nullArgument));
    }

    private static PullRequestActivity ConstructWithNull(string nullArgument) =>
        new(
            requestedReviewerLogins: nullArgument == "requestedReviewerLogins" ? null! : [],
            reviews: nullArgument == "reviews" ? null! : [],
            commits: nullArgument == "commits" ? null! : [],
            comments: nullArgument == "comments" ? null! : []
        );
}
