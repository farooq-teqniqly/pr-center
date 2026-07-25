using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PrCenter.Core.Derivation;
using PrCenter.Core.Facts;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Web.Components.Queue;

namespace PrCenter.Web.Tests.Queue;

public sealed class InboxViewTests : BunitContext
{
    private static readonly DateTimeOffset BaseInstant = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    private readonly QueueSnapshotHolder _holder = new(
        TimeProvider.System,
        NullLogger<QueueSnapshotHolder>.Instance
    );
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();

    public InboxViewTests()
    {
        Services.AddSingleton(_holder);
        Services.AddSingleton(new GetQueue(_holder));
        Services.AddSingleton(_trigger);
        Services.AddSingleton(TimeProvider.System);
    }

    [Fact]
    public void InboxView_GroupsByOwnerAndOrdersGroupsByOwnerStatusSequence()
    {
        // Arrange: items interleaved; owner statuses list ps-unite before PerfectServe.
        _holder.Publish(
            [
                Item("a", "PerfectServe", "repo1"),
                Item("b", "ps-unite", "repo2"),
                Item("c", "PerfectServe", "repo1"),
            ],
            [
                new OwnerStatus("ps-unite", OwnerFetchStatus.Ok),
                new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok),
            ]
        );

        // Act
        var cut = Render<InboxView>();

        // Assert
        Assert.Equal(["b", "a", "c"], RenderedPrIds(cut));
    }

    [Fact]
    public void InboxView_WhenOwnerStatusCasingDiffersFromItems_StillOrdersGroupsByThatSequence()
    {
        // Arrange: the status owner differs in case from the items' owner.
        _holder.Publish(
            [Item("a", "PerfectServe", "repo1"), Item("b", "ps-unite", "repo2")],
            [
                new OwnerStatus("PERFECTSERVE", OwnerFetchStatus.Ok),
                new OwnerStatus("ps-unite", OwnerFetchStatus.Ok),
            ]
        );

        // Act
        var cut = Render<InboxView>();

        // Assert
        Assert.Equal(["a", "b"], RenderedPrIds(cut));
    }

    [Fact]
    public void InboxView_WithinAGroup_OrdersUpdatedFirstThenMostRecent()
    {
        // Arrange
        _holder.Publish(
            [
                Item("old-no-update", "PerfectServe", "repo1", hasUpdate: false, at: BaseInstant),
                Item(
                    "new-update",
                    "PerfectServe",
                    "repo1",
                    hasUpdate: true,
                    at: BaseInstant.AddHours(1)
                ),
                Item(
                    "old-update",
                    "PerfectServe",
                    "repo1",
                    hasUpdate: true,
                    at: BaseInstant.AddHours(-1)
                ),
            ],
            [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]
        );

        // Act
        var cut = Render<InboxView>();

        // Assert
        Assert.Equal(["new-update", "old-update", "old-no-update"], RenderedPrIds(cut));
    }

    [Fact]
    public void InboxView_WhenANewSnapshotIsPublished_ReRenders()
    {
        // Arrange
        _holder.Publish(
            [Item("first", "PerfectServe", "repo1")],
            [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]
        );
        var cut = Render<InboxView>();

        // Act
        _holder.Publish(
            [Item("second", "PerfectServe", "repo1")],
            [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]
        );

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(["second"], RenderedPrIds(cut)));
    }

    [Fact]
    public async Task InboxView_WhenDisposed_UnsubscribesFromTheHolder()
    {
        // Arrange
        _holder.Publish(
            [Item("first", "PerfectServe", "repo1")],
            [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]
        );
        Render<InboxView>();

        // Act
        await DisposeComponentsAsync();

        // Assert: a publish after disposal must not fault a still-subscribed handler.
        var exception = Record.Exception(() =>
            _holder.Publish([], [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)])
        );
        Assert.Null(exception);
    }

    [Fact]
    public void InboxView_WhenRefreshClicked_PokesTheRefreshTrigger()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();

        // Act
        cut.Find("[data-testid=refresh]").Click();

        // Assert
        _trigger.Received(1).RequestRefresh();
    }

    [Fact]
    public void InboxView_WhenAllOwnersOk_ShowsOkChipsAndNoBanner()
    {
        // Arrange
        _holder.Publish(
            [Item("a", "PerfectServe", "repo1")],
            [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]
        );

        // Act
        var cut = Render<InboxView>();

        // Assert
        Assert.Single(cut.FindAll("[data-testid=owner-chip]"));
        Assert.Empty(cut.FindAll("[data-testid=error-banner]"));
    }

    [Fact]
    public void InboxView_WhenAnOwnerFails_ShowsBannerAndStillCarriesThatOwnersRows()
    {
        // Arrange
        _holder.Publish(
            [Item("a", "PerfectServe", "repo1"), Item("b", "ps-unite", "repo2")],
            [
                new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok),
                new OwnerStatus("ps-unite", OwnerFetchStatus.Error, "token rejected"),
            ]
        );

        // Act
        var cut = Render<InboxView>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid=error-banner]"));
        Assert.Equal(["a", "b"], RenderedPrIds(cut));
    }

    [Fact]
    public void InboxView_WhenPolledAndEmpty_ShowsAllCaughtUpWithOwnerChipsVisible()
    {
        // Arrange
        _holder.Publish([], [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]);

        // Act
        var cut = Render<InboxView>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid=empty-state]"));
        Assert.Single(cut.FindAll("[data-testid=owner-chip]"));
    }

    [Fact]
    public void InboxView_WhenNeverPolled_ShowsTheDistinctNeverPolledState()
    {
        // Arrange / Act
        var cut = Render<InboxView>();

        // Assert
        Assert.NotNull(cut.Find("[data-testid=never-polled]"));
        Assert.Empty(cut.FindAll("[data-testid=empty-state]"));
        Assert.Empty(cut.FindAll("[data-testid=owner-chip]"));
    }

    private static IReadOnlyList<string?> RenderedPrIds(IRenderedComponent<InboxView> cut) =>
        cut.FindAll("[data-testid=pr]").Select(e => e.GetAttribute("data-pr-id")).ToList();

    private static QueueItem Item(
        string id,
        string owner,
        string repository,
        bool hasUpdate = false,
        DateTimeOffset? at = null
    ) =>
        new(
            new PullRequestIdentity(
                id,
                owner,
                repository,
                1,
                "title",
                "https://example.test/pr",
                "author"
            ),
            new LastUpdate("octocat", at ?? BaseInstant),
            MembershipState.AwaitingFirstReview,
            hasUpdate,
            roster: [],
            new MyEngagement(lastReviewedAt: null),
            coveredBy: []
        );
}
