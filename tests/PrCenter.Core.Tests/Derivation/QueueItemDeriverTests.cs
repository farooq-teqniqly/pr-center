using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class QueueItemDeriverTests
{
    private const string MyLogin = TestLogins.Me;
    private const string Other = TestLogins.Other;

    private const string Draft = "draft";
    private const string Closed = "closed";
    private const string Approved = "approved";
    private const string Untracked = "untracked";

    [Fact]
    public void Derive_WhenShownWithUpdateAndCoverage_MapsAllDerivedValues()
    {
        // Arrange -- my earlier review sets the baseline; another's later commit updates
        var facts = TestFacts.Create(
            requested: [MyLogin],
            reviews: [new ReviewFact(MyLogin, ReviewState.ChangesRequested, TestTime.At(1))],
            commits: [new CommitFact(Other, TestTime.At(3))]
        );

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.NotNull(item);
        Assert.Same(facts.Identity, item.Identity);
        Assert.Equal(facts.Status.LastUpdatedBy, item.LastUpdate.By);
        Assert.Equal(facts.Status.LastUpdatedAt, item.LastUpdate.At);
        Assert.Equal(MembershipState.AwaitingReReview, item.State);
        Assert.True(item.HasUpdate);
    }

    [Fact]
    public void Derive_WhenShownWithNoOtherActivity_HasNoUpdateAndIsNotCovered()
    {
        // Arrange
        var facts = TestFacts.Create(requested: [MyLogin]);

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.NotNull(item);
        Assert.Equal(MembershipState.AwaitingFirstReview, item.State);
        Assert.False(item.HasUpdate);
        Assert.False(item.IsAlreadyCovered);
    }

    [Fact]
    public void Derive_HasUpdateMeasuredAgainstMyLastReview()
    {
        // Arrange -- another's commit landed before my latest review, so no update
        var facts = TestFacts.Create(
            requested: [MyLogin],
            reviews: [new ReviewFact(MyLogin, ReviewState.Commented, TestTime.At(3))],
            commits: [new CommitFact(Other, TestTime.At(2))]
        );

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.NotNull(item);
        Assert.False(item.HasUpdate);
    }

    [Fact]
    public void Derive_WhenINeverReviewedWithOthersActivity_HasNoUpdate()
    {
        // Arrange -- no review baseline of mine, so the pull request is new, not updated
        var facts = TestFacts.Create(
            requested: [MyLogin],
            commits: [new CommitFact(Other, TestTime.At(3))]
        );

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.NotNull(item);
        Assert.False(item.HasUpdate);
    }

    [Fact]
    public void Derive_LastReviewedIsGreatestOfMyReviewsRegardlessOfState()
    {
        // Arrange -- my commented-then-changes-requested reviews plus another reviewer's later one
        var facts = TestFacts.Create(
            requested: [MyLogin],
            reviews:
            [
                new ReviewFact(MyLogin, ReviewState.Commented, TestTime.At(1)),
                new ReviewFact(MyLogin, ReviewState.ChangesRequested, TestTime.At(3)),
                new ReviewFact(Other, ReviewState.Approved, TestTime.At(5)),
            ]
        );

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.NotNull(item);
        Assert.Equal(TestTime.At(3), item.MyEngagement.LastReviewedAt);
    }

    [Fact]
    public void Derive_WhenINeverReviewed_LastReviewedIsNull()
    {
        // Arrange
        var facts = TestFacts.Create(
            requested: [MyLogin],
            reviews: [new ReviewFact(Other, ReviewState.Approved, TestTime.At(1))]
        );

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.NotNull(item);
        Assert.Null(item.MyEngagement.LastReviewedAt);
    }

    [Fact]
    public void Derive_RidesRosterAndCoveringReviewersOnTheItem()
    {
        // Arrange
        var facts = TestFacts.Create(
            requested: [MyLogin],
            reviews: [new ReviewFact(Other, ReviewState.ChangesRequested, TestTime.At(1))]
        );

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.NotNull(item);
        Assert.Contains(item.Roster, entry => entry.Login == MyLogin && entry.IsMe);
        Assert.Contains(item.Roster, entry => entry.Login == Other);
        Assert.Equal([Other], item.CoveredBy);
    }

    [Theory]
    [InlineData(Draft)]
    [InlineData(Closed)]
    [InlineData(Approved)]
    [InlineData(Untracked)]
    public void Derive_WhenHidden_ReturnsNull(string scenario)
    {
        // Arrange
        var facts = HiddenFacts(scenario);

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.Null(item);
    }

    [Fact]
    public void Derive_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => QueueItemDeriver.Derive(null!, MyLogin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Derive_WithMissingMyLogin_Throws(string? myLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            QueueItemDeriver.Derive(TestFacts.Create(), myLogin!)
        );
    }

    private static PullRequestFacts HiddenFacts(string scenario) =>
        scenario switch
        {
            Draft => TestFacts.Create(isDraft: true, requested: [MyLogin]),
            Closed => TestFacts.Create(isClosedOrMerged: true, requested: [MyLogin]),
            Approved => TestFacts.Create(
                reviews: [new ReviewFact(MyLogin, ReviewState.Approved, TestTime.At(1))]
            ),
            Untracked => TestFacts.Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
}
