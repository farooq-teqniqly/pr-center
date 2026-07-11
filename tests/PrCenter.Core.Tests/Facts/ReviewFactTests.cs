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
        var review = new ReviewFact(TestLogins.Me, ReviewState.ChangesRequested, submittedAt);

        // Assert
        Assert.Equal(TestLogins.Me, review.ReviewerLogin);
        Assert.Equal(ReviewState.ChangesRequested, review.State);
        Assert.Equal(submittedAt, review.SubmittedAt);
        Assert.False(review.IsBot);
    }

    [Fact]
    public void Constructor_WithBotAuthor_SetsIsBot()
    {
        // Act
        var review = new ReviewFact(
            "qodo-code-review",
            ReviewState.Commented,
            DateTimeOffset.UtcNow,
            isBot: true
        );

        // Assert
        Assert.True(review.IsBot);
    }
}
