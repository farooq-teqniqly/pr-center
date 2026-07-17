using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class ReviewerRosterDeriverTests
{
    private const string MyLogin = TestLogins.Me;
    private const string Other = TestLogins.Other;

    [Fact]
    public void Derive_WhenReviewerOnlyRequested_IsPendingHumanEntry()
    {
        // Arrange
        var facts = TestFacts.Create(requested: [Other]);

        // Act
        var entry = Assert.Single(ReviewerRosterDeriver.Derive(facts, MyLogin));

        // Assert
        Assert.Equal(Other, entry.Login);
        Assert.Equal(ReviewerState.Pending, entry.State);
        Assert.False(entry.IsBot);
    }

    [Theory]
    [InlineData(ReviewState.Approved, ReviewerState.Approved)]
    [InlineData(ReviewState.ChangesRequested, ReviewerState.ChangesRequested)]
    [InlineData(ReviewState.Commented, ReviewerState.Commented)]
    public void Derive_WhenReviewerReviewed_TakesReviewState(
        ReviewState reviewState,
        ReviewerState expected
    )
    {
        // Arrange
        var facts = TestFacts.Create(reviews: [new ReviewFact(Other, reviewState, TestTime.At(1))]);

        // Act
        var entry = Assert.Single(ReviewerRosterDeriver.Derive(facts, MyLogin));

        // Assert
        Assert.Equal(expected, entry.State);
    }

    [Fact]
    public void Derive_WhenReviewerReviewedMultipleTimes_LatestStateWins()
    {
        // Arrange
        var facts = TestFacts.Create(
            reviews:
            [
                new ReviewFact(Other, ReviewState.Commented, TestTime.At(1)),
                new ReviewFact(Other, ReviewState.Approved, TestTime.At(3)),
                new ReviewFact(Other, ReviewState.ChangesRequested, TestTime.At(2)),
            ]
        );

        // Act
        var entry = Assert.Single(ReviewerRosterDeriver.Derive(facts, MyLogin));

        // Assert
        Assert.Equal(ReviewerState.Approved, entry.State);
    }

    [Fact]
    public void Derive_WhenReviewerRequestedAndReviewed_AppearsOnceWithReviewState()
    {
        // Arrange
        var facts = TestFacts.Create(
            requested: [Other],
            reviews: [new ReviewFact(Other, ReviewState.ChangesRequested, TestTime.At(1))]
        );

        // Act
        var entry = Assert.Single(ReviewerRosterDeriver.Derive(facts, MyLogin));

        // Assert
        Assert.Equal(ReviewerState.ChangesRequested, entry.State);
    }

    [Fact]
    public void Derive_WhenReviewerIsBot_KeepsEntryFlaggedAsBot()
    {
        // Arrange
        var facts = TestFacts.Create(
            reviews: [new ReviewFact("qodo", ReviewState.Commented, TestTime.At(1), isBot: true)]
        );

        // Act
        var entry = Assert.Single(ReviewerRosterDeriver.Derive(facts, MyLogin));

        // Assert
        Assert.Equal("qodo", entry.Login);
        Assert.True(entry.IsBot);
    }

    [Fact]
    public void Derive_WhenReviewerIsMe_SetsIsMe()
    {
        // Arrange
        var facts = TestFacts.Create(
            reviews:
            [
                new ReviewFact(MyLogin.ToUpperInvariant(), ReviewState.Approved, TestTime.At(1)),
                new ReviewFact(Other, ReviewState.Commented, TestTime.At(1)),
            ]
        );

        // Act
        var roster = ReviewerRosterDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.True(roster.Single(entry => entry.Login == MyLogin.ToUpperInvariant()).IsMe);
        Assert.False(roster.Single(entry => entry.Login == Other).IsMe);
    }

    [Fact]
    public void Derive_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ReviewerRosterDeriver.Derive(null!, MyLogin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Derive_WithMissingMyLogin_Throws(string? myLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            ReviewerRosterDeriver.Derive(TestFacts.Create(), myLogin!)
        );
    }
}
