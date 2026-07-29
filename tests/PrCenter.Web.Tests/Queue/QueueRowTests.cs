using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;
using PrCenter.Web.Components.Queue;

namespace PrCenter.Web.Tests.Queue;

public sealed class QueueRowTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _timeProvider = new(Now);

    public QueueRowTests() => Services.AddSingleton<TimeProvider>(_timeProvider);

    [Fact]
    public void QueueRow_WhenItemHasUpdate_ShowsStripeAndBadge()
    {
        // Arrange
        var item = Item(hasUpdate: true);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Contains("row-unseen", cut.Find("[data-testid=pr]").ClassList);
        Assert.NotNull(cut.Find("[data-testid=updated-badge]"));
    }

    [Fact]
    public void QueueRow_WhenNeverReviewed_RendersWithoutBadgeButStillRenders()
    {
        // Arrange
        var item = Item(hasUpdate: false);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.DoesNotContain("row-unseen", cut.Find("[data-testid=pr]").ClassList);
        Assert.Empty(cut.FindAll("[data-testid=updated-badge]"));
        Assert.Equal("pr-1", cut.Find("[data-testid=pr]").GetAttribute("data-pr-id"));
    }

    [Fact]
    public void QueueRow_Byline_IsLastUpdateByThenRelativeTime()
    {
        // Arrange
        var item = Item(lastUpdateBy: "dkellner", lastUpdateAt: Now.AddMinutes(-22));

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Contains(
            "dkellner",
            cut.Find("[data-testid=byline]").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "22m ago",
            cut.Find("[data-testid=byline]").TextContent,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void QueueRow_WhenCovered_NamesTheCoveringReviewers()
    {
        // Arrange
        var item = Item(coveredBy: ["mprysork", "jay"]);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Contains(
            "mprysork, jay",
            cut.Find("[data-testid=covered-badge]").TextContent,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void QueueRow_RendersLastReviewedAndLastUpdateInstants()
    {
        // Arrange
        var item = Item(lastReviewedAt: Now.AddHours(-2), lastUpdateAt: Now.AddMinutes(-5));

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Contains(
            "2h ago",
            cut.Find("[data-testid=last-reviewed-at]").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "5m ago",
            cut.Find("[data-testid=last-update-at]").TextContent,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void QueueRow_WhenNeverReviewed_ShowsNeverForLastReviewed()
    {
        // Arrange
        var item = Item(lastReviewedAt: null);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Contains(
            "never",
            cut.Find("[data-testid=last-reviewed-at]").TextContent,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void QueueRow_WhenAuthoredByMe_ShowsTheMineBadge()
    {
        // Arrange
        var item = Item(authoredByMe: true);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Contains(
            "mine",
            cut.Find("[data-testid=mine-badge]").TextContent,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void QueueRow_WhenAuthoredByAnother_ShowsNoMineBadge()
    {
        // Arrange
        var item = Item(authoredByMe: false);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=mine-badge]"));
    }

    [Fact]
    public void QueueRow_WhenAuthoredByMe_ShadesTheRow()
    {
        // Arrange
        var item = Item(authoredByMe: true);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Contains("row-mine", cut.Find("[data-testid=pr]").ClassList);
    }

    [Fact]
    public void QueueRow_WhenAuthoredByAnother_DoesNotShadeTheRow()
    {
        // Arrange
        var item = Item(authoredByMe: false);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.DoesNotContain("row-mine", cut.Find("[data-testid=pr]").ClassList);
    }

    [Fact]
    public void QueueRow_Title_IsPlainAnchorToIdentityUrlWithNoSideEffect()
    {
        // Arrange
        var item = Item(url: "https://github.test/org/repo/pull/1");

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));
        var link = cut.Find("[data-testid=pr-title-link]");

        // Assert
        Assert.Equal("https://github.test/org/repo/pull/1", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.False(link.HasAttribute("onclick"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("http://github.test/org/repo/pull/1")]
    [InlineData("not a url")]
    public void QueueRow_WhenTheUrlIsNotHttps_RendersTheTitleAsTextWithNoAnchor(string url)
    {
        // Arrange
        var item = Item(url: url);

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=pr-title-link]"));
        Assert.Equal("title", cut.Find("[data-testid=pr-title-text]").TextContent);
    }

    [Fact]
    public void QueueRow_WhenTheClockAdvancesBetweenReads_FormatsEveryInstantFromOneRenderTime()
    {
        // Arrange
        _timeProvider.AutoAdvanceAmount = TimeSpan.FromMinutes(30);
        var item = Item(lastUpdateAt: Now.AddMinutes(-5), lastReviewedAt: Now.AddMinutes(-5));

        // Act
        var cut = Render<QueueRow>(ps => ps.Add(p => p.Item, item));

        // Assert
        Assert.Contains(
            "5m ago",
            cut.Find("[data-testid=byline]").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "5m ago",
            cut.Find("[data-testid=last-update-at]").TextContent,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "5m ago",
            cut.Find("[data-testid=last-reviewed-at]").TextContent,
            StringComparison.Ordinal
        );
    }

    private static QueueItem Item(
        bool hasUpdate = false,
        bool authoredByMe = false,
        string lastUpdateBy = "octocat",
        DateTimeOffset? lastUpdateAt = null,
        DateTimeOffset? lastReviewedAt = null,
        IReadOnlyList<string>? coveredBy = null,
        string url = "https://example.test/pr"
    ) =>
        new(
            new PullRequestIdentity("pr-1", "owner", "repo", 1, "title", url, "author"),
            new LastUpdate(lastUpdateBy, lastUpdateAt ?? Now),
            new QueueItemStatus(MembershipState.AwaitingFirstReview, hasUpdate, authoredByMe),
            roster: [],
            new MyEngagement(lastReviewedAt),
            coveredBy: coveredBy ?? []
        );
}
