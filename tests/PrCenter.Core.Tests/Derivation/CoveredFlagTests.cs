using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class CoveredFlagTests
{
    private const string MyLogin = TestLogins.Me;
    private const string Other = TestLogins.Other;

    [Theory]
    [InlineData(ReviewState.Approved)]
    [InlineData(ReviewState.ChangesRequested)]
    [InlineData(ReviewState.Commented)]
    public void IsCovered_WhenAnotherReviewerHasReviewed_ReturnsTrue(ReviewState state)
    {
        // Arrange
        var facts = TestFacts.Create(reviews: [new ReviewFact(Other, state, TestTime.At(1))]);

        // Act
        var covered = CoveredFlag.IsCovered(facts, MyLogin);

        // Assert
        Assert.True(covered);
    }

    [Fact]
    public void IsCovered_WhenOnlyPendingRequests_ReturnsFalse()
    {
        // Arrange
        var facts = TestFacts.Create(requested: [Other]);

        // Act
        var covered = CoveredFlag.IsCovered(facts, MyLogin);

        // Assert
        Assert.False(covered);
    }

    [Fact]
    public void IsCovered_WhenOnlyOwnReviews_ReturnsFalse()
    {
        // Arrange
        var facts = TestFacts.Create(
            reviews: [new ReviewFact(MyLogin, ReviewState.ChangesRequested, TestTime.At(1))]
        );

        // Act
        var covered = CoveredFlag.IsCovered(facts, MyLogin);

        // Assert
        Assert.False(covered);
    }

    [Fact]
    public void IsCovered_WhenOnlyBotReviewsByOthers_ReturnsFalse()
    {
        // Arrange
        var facts = TestFacts.Create(
            reviews:
            [
                new ReviewFact(Other, ReviewState.ChangesRequested, TestTime.At(1), isBot: true),
            ]
        );

        // Act
        var covered = CoveredFlag.IsCovered(facts, MyLogin);

        // Assert
        Assert.False(covered);
    }

    [Fact]
    public void IsCovered_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => CoveredFlag.IsCovered(null!, MyLogin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsCovered_WithMissingMyLogin_Throws(string? myLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            CoveredFlag.IsCovered(TestFacts.Create(), myLogin!)
        );
    }
}
