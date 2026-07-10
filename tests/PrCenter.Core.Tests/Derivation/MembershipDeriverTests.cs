using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class MembershipDeriverTests
{
    private const string MyLogin = "octocat";

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
    public void Derive_WhenLatestReviewIsApprovalAfterAComment_AwaitsReReview()
    {
        // Arrange
        var reviews = new[]
        {
            Review(MyLogin, ReviewState.Approved, At(1)),
            Review(MyLogin, ReviewState.Commented, At(2)),
        };
        var facts = Facts(reviews: reviews);

        // Act
        var result = MembershipDeriver.Derive(facts, MyLogin);

        // Assert
        Assert.Equal(MembershipResult.Shown(MembershipState.AwaitingReReview), result);
    }

    [Fact]
    public void Derive_WhenRequestedLoginDiffersOnlyByCase_MatchesTheUser()
    {
        // Arrange
        var facts = Facts(requested: ["OCTOCAT"]);

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
        Assert.ThrowsAny<ArgumentException>(() => MembershipDeriver.Derive(Facts(), myLogin!));
    }

    private static (PullRequestFacts Facts, MembershipResult Expected) Scenario(string scenario) =>
        scenario switch
        {
            Draft => (
                Facts(isDraft: true, requested: [MyLogin]),
                MembershipResult.Hidden(MembershipExclusion.Draft)
            ),
            Closed => (
                Facts(
                    isClosedOrMerged: true,
                    reviews: [Review(MyLogin, ReviewState.ChangesRequested, At(1))]
                ),
                MembershipResult.Hidden(MembershipExclusion.ClosedOrMerged)
            ),
            RequestedNoReview => (
                Facts(requested: [MyLogin]),
                MembershipResult.Shown(MembershipState.AwaitingFirstReview)
            ),
            CommentedNotRequested => (
                Facts(reviews: [Review(MyLogin, ReviewState.Commented, At(1))]),
                MembershipResult.Shown(MembershipState.AwaitingReReview)
            ),
            ChangesRequestedRequested => (
                Facts(
                    requested: [MyLogin],
                    reviews: [Review(MyLogin, ReviewState.ChangesRequested, At(1))]
                ),
                MembershipResult.Shown(MembershipState.AwaitingReReview)
            ),
            ApprovedNotRequested => (
                Facts(reviews: [Review(MyLogin, ReviewState.Approved, At(1))]),
                MembershipResult.Hidden(MembershipExclusion.Approved)
            ),
            ApprovedRequested => (
                Facts(
                    requested: [MyLogin],
                    reviews: [Review(MyLogin, ReviewState.Approved, At(1))]
                ),
                MembershipResult.Shown(MembershipState.AwaitingFirstReview)
            ),
            NotRequestedNoReview => (
                Facts(),
                MembershipResult.Hidden(MembershipExclusion.Untracked)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };

    private static PullRequestFacts Facts(
        bool isDraft = false,
        bool isClosedOrMerged = false,
        IReadOnlyList<string>? requested = null,
        IReadOnlyList<ReviewFact>? reviews = null
    ) =>
        new(
            new PullRequestIdentity(
                id: "owner/repo#1",
                owner: "owner",
                repository: "repo",
                number: 1,
                title: "Add feature",
                url: "https://github.com/owner/repo/pull/1"
            ),
            new PullRequestStatus(
                isDraft: isDraft,
                isClosedOrMerged: isClosedOrMerged,
                lastUpdatedBy: "author",
                lastUpdatedAt: At(1)
            ),
            new PullRequestActivity(requested ?? [], reviews ?? [], [], [])
        );

    private static ReviewFact Review(string login, ReviewState state, DateTimeOffset at) =>
        new(login, state, at);

    private static DateTimeOffset At(int hour) => new(2026, 1, 1, hour, 0, 0, TimeSpan.Zero);
}
