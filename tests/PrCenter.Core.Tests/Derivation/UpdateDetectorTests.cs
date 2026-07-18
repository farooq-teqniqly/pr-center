using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class UpdateDetectorTests
{
    private const string MyLogin = TestLogins.Me;
    private const string Other = TestLogins.Other;

    private const string Commit = "commit";
    private const string Comment = "comment";
    private const string Review = "review";

    private static readonly DateTimeOffset MyLastReviewedAt = TestTime.At(2);

    [Fact]
    public void HasUpdate_WhenNeverReviewed_ReturnsFalse()
    {
        // Arrange -- other people's activity is present, but with no review
        // baseline the pull request is new, not updated
        var facts = TestFacts.Create(
            commits: [new CommitFact(Other, TestTime.At(3))],
            comments: [new CommentFact(Other, TestTime.At(3))],
            reviews: [new ReviewFact(Other, ReviewState.Commented, TestTime.At(3))]
        );

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, myLastReviewedAt: null);

        // Assert
        Assert.False(hasUpdate);
    }

    [Theory]
    [InlineData(Commit)]
    [InlineData(Comment)]
    [InlineData(Review)]
    public void HasUpdate_WhenOtherPersonActivityAfterMyLastReview_ReturnsTrue(string eventType)
    {
        // Arrange
        var facts = FactsWithOtherEventAt(eventType, TestTime.At(3));

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, MyLastReviewedAt);

        // Assert
        Assert.True(hasUpdate);
    }

    [Theory]
    [InlineData(Commit)]
    [InlineData(Comment)]
    [InlineData(Review)]
    public void HasUpdate_WhenOtherPersonActivityAtOrBeforeMyLastReview_ReturnsFalse(
        string eventType
    )
    {
        // Arrange
        var atReview = FactsWithOtherEventAt(eventType, TestTime.At(2));
        var beforeReview = FactsWithOtherEventAt(eventType, TestTime.At(1));

        // Act
        var atResult = UpdateDetector.HasUpdate(atReview, MyLogin, MyLastReviewedAt);
        var beforeResult = UpdateDetector.HasUpdate(beforeReview, MyLogin, MyLastReviewedAt);

        // Assert
        Assert.False(atResult);
        Assert.False(beforeResult);
    }

    [Fact]
    public void HasUpdate_WhenOnlyOwnActivityAfterMyLastReview_ReturnsFalse()
    {
        // Arrange
        var facts = TestFacts.Create(
            commits: [new CommitFact(MyLogin, TestTime.At(3))],
            comments: [new CommentFact(MyLogin, TestTime.At(3))],
            reviews: [new ReviewFact(MyLogin, ReviewState.Commented, TestTime.At(3))]
        );

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, MyLastReviewedAt);

        // Assert
        Assert.False(hasUpdate);
    }

    [Fact]
    public void HasUpdate_WhenOnlyBotCommentsAndReviewsAfterMyLastReview_ReturnsFalse()
    {
        // Arrange
        var facts = TestFacts.Create(
            comments: [new CommentFact(Other, TestTime.At(3), isBot: true)],
            reviews: [new ReviewFact(Other, ReviewState.Commented, TestTime.At(3), isBot: true)]
        );

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, MyLastReviewedAt);

        // Assert
        Assert.False(hasUpdate);
    }

    [Fact]
    public void HasUpdate_WhenBotCommitAccompaniesIgnoredBotActivity_ReturnsTrue()
    {
        // Arrange -- the bot comment and review are ignored, but the commit counts
        var facts = TestFacts.Create(
            commits: [new CommitFact(Other, TestTime.At(3))],
            comments: [new CommentFact(Other, TestTime.At(3), isBot: true)],
            reviews: [new ReviewFact(Other, ReviewState.Commented, TestTime.At(3), isBot: true)]
        );

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, MyLastReviewedAt);

        // Assert
        Assert.True(hasUpdate);
    }

    [Fact]
    public void HasUpdate_WhenNoActivitySinceMyLastReview_ReturnsFalse()
    {
        // Arrange
        var facts = TestFacts.Create();

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, MyLastReviewedAt);

        // Assert
        Assert.False(hasUpdate);
    }

    [Fact]
    public void HasUpdate_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            UpdateDetector.HasUpdate(null!, MyLogin, MyLastReviewedAt)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void HasUpdate_WithMissingMyLogin_Throws(string? myLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            UpdateDetector.HasUpdate(TestFacts.Create(), myLogin!, MyLastReviewedAt)
        );
    }

    private static PullRequestFacts FactsWithOtherEventAt(string eventType, DateTimeOffset at) =>
        eventType switch
        {
            Commit => TestFacts.Create(commits: [new CommitFact(Other, at)]),
            Comment => TestFacts.Create(comments: [new CommentFact(Other, at)]),
            Review => TestFacts.Create(reviews: [new ReviewFact(Other, ReviewState.Commented, at)]),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
        };
}
