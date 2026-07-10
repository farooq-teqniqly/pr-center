using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class UpdateDetectorTests
{
    private const string MyLogin = "octocat";
    private const string Other = "hubot";

    private const string Commit = "commit";
    private const string Comment = "comment";
    private const string Review = "review";

    private static readonly DateTimeOffset Marker = At(2);

    [Fact]
    public void HasUpdate_WhenNeverSeen_ReturnsTrue()
    {
        // Arrange
        var facts = Facts();

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, lastSeen: null);

        // Assert
        Assert.True(hasUpdate);
    }

    [Theory]
    [InlineData(Commit)]
    [InlineData(Comment)]
    [InlineData(Review)]
    public void HasUpdate_WhenOtherPersonActivityAfterMarker_ReturnsTrue(string eventType)
    {
        // Arrange
        var facts = FactsWithOtherEventAt(eventType, At(3));

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, Marker);

        // Assert
        Assert.True(hasUpdate);
    }

    [Theory]
    [InlineData(Commit)]
    [InlineData(Comment)]
    [InlineData(Review)]
    public void HasUpdate_WhenOtherPersonActivityAtOrBeforeMarker_ReturnsFalse(string eventType)
    {
        // Arrange
        var atMarker = FactsWithOtherEventAt(eventType, At(2));
        var beforeMarker = FactsWithOtherEventAt(eventType, At(1));

        // Act
        var atResult = UpdateDetector.HasUpdate(atMarker, MyLogin, Marker);
        var beforeResult = UpdateDetector.HasUpdate(beforeMarker, MyLogin, Marker);

        // Assert
        Assert.False(atResult);
        Assert.False(beforeResult);
    }

    [Fact]
    public void HasUpdate_WhenOnlyOwnActivityAfterMarker_ReturnsFalse()
    {
        // Arrange
        var facts = Facts(
            commits: [new CommitFact(MyLogin, At(3))],
            comments: [new CommentFact(MyLogin, At(3))],
            reviews: [new ReviewFact(MyLogin, ReviewState.Commented, At(3))]
        );

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, Marker);

        // Assert
        Assert.False(hasUpdate);
    }

    [Fact]
    public void HasUpdate_WhenNoActivitySinceMarker_ReturnsFalse()
    {
        // Arrange
        var facts = Facts();

        // Act
        var hasUpdate = UpdateDetector.HasUpdate(facts, MyLogin, Marker);

        // Assert
        Assert.False(hasUpdate);
    }

    [Fact]
    public void HasUpdate_WithNullFacts_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            UpdateDetector.HasUpdate(null!, MyLogin, Marker)
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
            UpdateDetector.HasUpdate(Facts(), myLogin!, Marker)
        );
    }

    private static PullRequestFacts FactsWithOtherEventAt(string eventType, DateTimeOffset at) =>
        eventType switch
        {
            Commit => Facts(commits: [new CommitFact(Other, at)]),
            Comment => Facts(comments: [new CommentFact(Other, at)]),
            Review => Facts(reviews: [new ReviewFact(Other, ReviewState.Commented, at)]),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null),
        };

    private static PullRequestFacts Facts(
        IReadOnlyList<CommitFact>? commits = null,
        IReadOnlyList<CommentFact>? comments = null,
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
                isDraft: false,
                isClosedOrMerged: false,
                lastUpdatedBy: "author",
                lastUpdatedAt: At(1)
            ),
            new PullRequestActivity([], reviews ?? [], commits ?? [], comments ?? [])
        );

    private static DateTimeOffset At(int hour) => new(2026, 1, 1, hour, 0, 0, TimeSpan.Zero);
}
