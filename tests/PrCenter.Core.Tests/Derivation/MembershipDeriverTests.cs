using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class MembershipDeriverTests
{
    private const string MyLogin = TestLogins.Me;

    private const string Draft = "draft";
    private const string Closed = "closed";
    private const string RequestedNoReview = "requestedNoReview";
    private const string CommentedNotRequested = "commentedNotRequested";
    private const string ChangesRequestedRequested = "changesRequestedRequested";
    private const string ApprovedNotRequested = "approvedNotRequested";
    private const string ApprovedRequested = "approvedRequested";
    private const string NotRequestedNoReview = "notRequestedNoReview";

    [Theory]
    [InlineData(Draft)]
    [InlineData(Closed)]
    [InlineData(RequestedNoReview)]
    [InlineData(CommentedNotRequested)]
    [InlineData(ChangesRequestedRequested)]
    [InlineData(ApprovedNotRequested)]
    [InlineData(ApprovedRequested)]
    [InlineData(NotRequestedNoReview)]
    public void Derive_ForScenario_ReturnsExpectedMembership(string scenario)
    {
        // Arrange
        var (facts, expected) = Scenario(scenario);

        // Act
        var result = MembershipDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Derive_WhenACommentFollowsAnEarlierApproval_AwaitsReReview()
    {
        // Arrange
        var reviews = new[]
        {
            Review(MyLogin, ReviewState.Approved, TestTime.At(1)),
            Review(MyLogin, ReviewState.Commented, TestTime.At(2)),
        };
        var facts = TestFacts.Create(reviews: reviews);

        // Act
        var result = MembershipDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.Equal(MembershipResult.Shown(MembershipState.AwaitingReReview), result);
    }

    [Theory]
    [InlineData(ReviewState.Commented)]
    [InlineData(ReviewState.ChangesRequested)]
    public void Derive_WhenApprovalTiesANonApprovedReviewOnTimestamp_AwaitsReReview(
        ReviewState nonApproved
    )
    {
        // Arrange
        var reviews = new[]
        {
            Review(MyLogin, ReviewState.Approved, TestTime.At(2)),
            Review(MyLogin, nonApproved, TestTime.At(2)),
        };
        var facts = TestFacts.Create(reviews: reviews);

        // Act
        var result = MembershipDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.Equal(MembershipResult.Shown(MembershipState.AwaitingReReview), result);
    }

    [Fact]
    public void Derive_WhenApprovalIsStrictlyLaterThanAComment_DropsThePullRequest()
    {
        // Arrange
        var reviews = new[]
        {
            Review(MyLogin, ReviewState.Commented, TestTime.At(1)),
            Review(MyLogin, ReviewState.Approved, TestTime.At(2)),
        };
        var facts = TestFacts.Create(reviews: reviews);

        // Act
        var result = MembershipDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.Equal(MembershipResult.Hidden(MembershipExclusion.Approved), result);
    }

    [Fact]
    public void Derive_WhenRequestedLoginDiffersOnlyByCase_MatchesTheUser()
    {
        // Arrange
        var facts = TestFacts.Create(requested: [TestLogins.Me.ToUpperInvariant()]);

        // Act
        var result = MembershipDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.Equal(MembershipResult.Shown(MembershipState.AwaitingFirstReview), result);
    }

    [Fact]
    public void Derive_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => MembershipDeriver.Derive(null!, MyLogin));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Derive_WithMissingMyLogin_Throws(string? myLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            MembershipDeriver.Derive(TestFacts.Create(), myLogin!)
        );
    }

    private static (PullRequestFacts Facts, MembershipResult Expected) Scenario(string scenario) =>
        scenario switch
        {
            Draft => (
                TestFacts.Create(isDraft: true, requested: [MyLogin]),
                MembershipResult.Hidden(MembershipExclusion.Draft)
            ),
            Closed => (
                TestFacts.Create(
                    isClosedOrMerged: true,
                    reviews: [Review(MyLogin, ReviewState.ChangesRequested, TestTime.At(1))]
                ),
                MembershipResult.Hidden(MembershipExclusion.ClosedOrMerged)
            ),
            RequestedNoReview => (
                TestFacts.Create(requested: [MyLogin]),
                MembershipResult.Shown(MembershipState.AwaitingFirstReview)
            ),
            CommentedNotRequested => (
                TestFacts.Create(reviews: [Review(MyLogin, ReviewState.Commented, TestTime.At(1))]),
                MembershipResult.Shown(MembershipState.AwaitingReReview)
            ),
            ChangesRequestedRequested => (
                TestFacts.Create(
                    requested: [MyLogin],
                    reviews: [Review(MyLogin, ReviewState.ChangesRequested, TestTime.At(1))]
                ),
                MembershipResult.Shown(MembershipState.AwaitingReReview)
            ),
            ApprovedNotRequested => (
                TestFacts.Create(reviews: [Review(MyLogin, ReviewState.Approved, TestTime.At(1))]),
                MembershipResult.Hidden(MembershipExclusion.Approved)
            ),
            ApprovedRequested => (
                TestFacts.Create(
                    requested: [MyLogin],
                    reviews: [Review(MyLogin, ReviewState.Approved, TestTime.At(1))]
                ),
                MembershipResult.Shown(MembershipState.AwaitingFirstReview)
            ),
            NotRequestedNoReview => (
                TestFacts.Create(),
                MembershipResult.Hidden(MembershipExclusion.Untracked)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

    private static ReviewFact Review(string login, ReviewState state, DateTimeOffset at) =>
        new(login, state, at);
}
