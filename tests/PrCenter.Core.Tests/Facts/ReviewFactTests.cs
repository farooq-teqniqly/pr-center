using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Facts;

public sealed class ReviewFactTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithMissingReviewerLogin_Throws(string? reviewerLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            new ReviewFact(reviewerLogin!, ReviewState.Approved, DateTimeOffset.UtcNow)
        );
    }

    [Fact]
    public void Constructor_WithValidArguments_ExposesValues()
    {
        // Arrange
        var submittedAt = DateTimeOffset.UtcNow;

        // Act
        var review = new ReviewFact("octocat", ReviewState.ChangesRequested, submittedAt);

        // Assert
        Assert.Equal("octocat", review.ReviewerLogin);
        Assert.Equal(ReviewState.ChangesRequested, review.State);
        Assert.Equal(submittedAt, review.SubmittedAt);
    }
}
