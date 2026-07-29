using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

public sealed class QueueItemTests
{
    [Fact]
    public void Constructor_WithNullIdentity_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new QueueItem(
                null!,
                ValidLastUpdate(),
                ValidStatus(),
                ValidRoster(),
                ValidEngagement(),
                ValidCoveredBy()
            )
        );
    }

    [Fact]
    public void Constructor_WithNullLastUpdate_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new QueueItem(
                ValidIdentity(),
                null!,
                ValidStatus(),
                ValidRoster(),
                ValidEngagement(),
                ValidCoveredBy()
            )
        );
    }

    [Fact]
    public void Constructor_WithNullStatus_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new QueueItem(
                ValidIdentity(),
                ValidLastUpdate(),
                null!,
                ValidRoster(),
                ValidEngagement(),
                ValidCoveredBy()
            )
        );
    }

    [Fact]
    public void Constructor_WithNullRoster_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new QueueItem(
                ValidIdentity(),
                ValidLastUpdate(),
                ValidStatus(),
                null!,
                ValidEngagement(),
                ValidCoveredBy()
            )
        );
    }

    [Fact]
    public void Constructor_WithNullMyEngagement_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new QueueItem(
                ValidIdentity(),
                ValidLastUpdate(),
                ValidStatus(),
                ValidRoster(),
                null!,
                ValidCoveredBy()
            )
        );
    }

    [Fact]
    public void Constructor_WithNullCoveredBy_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            new QueueItem(
                ValidIdentity(),
                ValidLastUpdate(),
                ValidStatus(),
                ValidRoster(),
                ValidEngagement(),
                null!
            )
        );
    }

    [Fact]
    public void State_AfterConstruction_ReflectsStatus()
    {
        // Act
        var item = Build(
            status: new QueueItemStatus(
                MembershipState.AwaitingReReview,
                hasUpdate: true,
                authoredByMe: false
            )
        );

        // Assert
        Assert.Equal(MembershipState.AwaitingReReview, item.State);
        Assert.True(item.HasUpdate);
    }

    [Fact]
    public void AuthoredByMe_WhenStatusFlagSet_IsTrue()
    {
        // Act
        var item = Build(
            status: new QueueItemStatus(
                MembershipState.AwaitingFirstReview,
                hasUpdate: false,
                authoredByMe: true
            )
        );

        // Assert
        Assert.True(item.AuthoredByMe);
    }

    [Fact]
    public void AuthoredByMe_WhenStatusFlagClear_IsFalse()
    {
        // Act
        var item = Build(
            status: new QueueItemStatus(
                MembershipState.AwaitingFirstReview,
                hasUpdate: false,
                authoredByMe: false
            )
        );

        // Assert
        Assert.False(item.AuthoredByMe);
    }

    [Fact]
    public void IsAlreadyCovered_WhenCoveredByHasReviewers_IsTrue()
    {
        // Act
        var item = Build(coveredBy: [TestLogins.Other]);

        // Assert
        Assert.True(item.IsAlreadyCovered);
    }

    [Fact]
    public void IsAlreadyCovered_WhenCoveredByEmpty_IsFalse()
    {
        // Act
        var item = Build(coveredBy: []);

        // Assert
        Assert.False(item.IsAlreadyCovered);
    }

    [Fact]
    public void Roster_AfterConstruction_IsReadOnlyCopyOfTheSource()
    {
        // Arrange
        var source = new List<ReviewerRosterEntry>
        {
            new(TestLogins.Other, ReviewerState.Pending, isBot: false, isMe: false),
        };
        var item = Build(roster: source);

        // Act
        source.Clear();

        // Assert
        Assert.Single(item.Roster);
    }

    [Fact]
    public void CoveredBy_AfterConstruction_IsReadOnlyCopyOfTheSource()
    {
        // Arrange
        var source = new List<string> { TestLogins.Other };
        var item = Build(coveredBy: source);

        // Act
        source.Clear();

        // Assert
        Assert.Single(item.CoveredBy);
    }

    private static QueueItem Build(
        QueueItemStatus? status = null,
        IReadOnlyList<ReviewerRosterEntry>? roster = null,
        IReadOnlyList<string>? coveredBy = null
    ) =>
        new(
            ValidIdentity(),
            ValidLastUpdate(),
            status ?? ValidStatus(),
            roster ?? ValidRoster(),
            ValidEngagement(),
            coveredBy ?? ValidCoveredBy()
        );

    private static PullRequestIdentity ValidIdentity() =>
        new(
            id: "owner/repo#1",
            owner: "owner",
            repository: "repo",
            number: 1,
            title: "Add feature",
            url: "https://github.com/owner/repo/pull/1",
            authorLogin: TestLogins.Author
        );

    private static LastUpdate ValidLastUpdate() => new(TestLogins.Author, TestTime.At(1));

    private static QueueItemStatus ValidStatus() =>
        new(MembershipState.AwaitingFirstReview, hasUpdate: false, authoredByMe: false);

    private static IReadOnlyList<ReviewerRosterEntry> ValidRoster() => [];

    private static MyEngagement ValidEngagement() => new(lastReviewedAt: null);

    private static IReadOnlyList<string> ValidCoveredBy() => [];
}
