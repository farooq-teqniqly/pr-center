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

    private static readonly DateTimeOffset Marker = TestTime.At(2);

    [Fact]
    public void Derive_WhenShownWithUpdateAndCoverage_MapsAllDerivedValues()
    {
        // Arrange
        var facts = TestFacts.Create(
            requested: [MyLogin],
            reviews: [new ReviewFact(Other, ReviewState.ChangesRequested, TestTime.At(1))],
            commits: [new CommitFact(Other, TestTime.At(3))]
        );

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin, Marker);

        // Assert
        Assert.NotNull(item);
        Assert.Same(facts.Identity, item.Identity);
        Assert.Equal(facts.Status.LastUpdatedBy, item.LastUpdatedBy);
        Assert.Equal(facts.Status.LastUpdatedAt, item.LastUpdatedAt);
        Assert.Equal(MembershipState.AwaitingFirstReview, item.State);
        Assert.True(item.HasUpdate);
        Assert.True(item.IsAlreadyCovered);
    }

    [Fact]
    public void Derive_WhenShownWithNoOtherActivity_HasNoUpdateAndIsNotCovered()
    {
        // Arrange
        var facts = TestFacts.Create(requested: [MyLogin]);

        // Act
        var item = QueueItemDeriver.Derive(facts, MyLogin, Marker);

        // Assert
        Assert.NotNull(item);
        Assert.Equal(MembershipState.AwaitingFirstReview, item.State);
        Assert.False(item.HasUpdate);
        Assert.False(item.IsAlreadyCovered);
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
        var item = QueueItemDeriver.Derive(facts, MyLogin, Marker);

        // Assert
        Assert.Null(item);
    }

    [Fact]
    public void Derive_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => QueueItemDeriver.Derive(null!, MyLogin, Marker));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Derive_WithMissingMyLogin_Throws(string? myLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            QueueItemDeriver.Derive(TestFacts.Create(), myLogin!, Marker)
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
