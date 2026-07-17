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
    public void CoveringLogins_WhenAnotherReviewerHasReviewed_YieldsTheirLogin(ReviewState state)
    {
        // Arrange
        var facts = TestFacts.Create(reviews: [new ReviewFact(Other, state, TestTime.At(1))]);

        // Act
        var covering = CoveredFlag.CoveringLogins(facts, MyLogin);

        // Assert
        Assert.Equal([Other], covering);
    }

    [Fact]
    public void CoveringLogins_WhenReviewerReviewedRepeatedly_YieldsLoginOnce()
    {
        // Arrange
        var facts = TestFacts.Create(
            reviews:
            [
                new ReviewFact(Other, ReviewState.Commented, TestTime.At(1)),
                new ReviewFact(Other, ReviewState.Approved, TestTime.At(2)),
            ]
        );

        // Act
        var covering = CoveredFlag.CoveringLogins(facts, MyLogin);

        // Assert
        Assert.Equal([Other], covering);
    }

    [Fact]
    public void CoveringLogins_WhenOnlyPendingRequests_IsEmpty()
    {
        // Arrange
        var facts = TestFacts.Create(requested: [Other]);

        // Act
        var covering = CoveredFlag.CoveringLogins(facts, MyLogin);

        // Assert
        Assert.Empty(covering);
    }

    [Fact]
    public void CoveringLogins_WhenOnlyOwnReviews_IsEmpty()
    {
        // Arrange
        var facts = TestFacts.Create(
            reviews: [new ReviewFact(MyLogin, ReviewState.ChangesRequested, TestTime.At(1))]
        );

        // Act
        var covering = CoveredFlag.CoveringLogins(facts, MyLogin);

        // Assert
        Assert.Empty(covering);
    }

    [Fact]
    public void CoveringLogins_WhenOnlyBotReviewsByOthers_IsEmpty()
    {
        // Arrange
        var facts = TestFacts.Create(
            reviews:
            [
                new ReviewFact(Other, ReviewState.ChangesRequested, TestTime.At(1), isBot: true),
            ]
        );

        // Act
        var covering = CoveredFlag.CoveringLogins(facts, MyLogin);

        // Assert
        Assert.Empty(covering);
    }

    [Fact]
    public void CoveringLogins_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => CoveredFlag.CoveringLogins(null!, MyLogin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CoveringLogins_WithMissingMyLogin_Throws(string? myLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            CoveredFlag.CoveringLogins(TestFacts.Create(), myLogin!)
        );
    }
}
