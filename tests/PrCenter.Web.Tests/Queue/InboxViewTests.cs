using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
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

    private readonly CapturingLogger<QueueSnapshotHolder> _logger = new();
    private readonly QueueSnapshotHolder _holder;
    private readonly RefreshStateHolder _refreshState;
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();
    private readonly FakeTimeProvider _clock = new();

    public InboxViewTests()
    {
        _holder = new QueueSnapshotHolder(TimeProvider.System, _logger);
        _clock.SetUtcNow(BaseInstant);
        _refreshState = new RefreshStateHolder(_clock, new CapturingLogger<RefreshStateHolder>());
        Services.AddSingleton(_holder);
        Services.AddSingleton(_refreshState);
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

    // Named for what it can actually observe. Unsubscription itself has no
    // observable signal through the public surface: a disposed bUnit renderer
    // silently ignores InvokeAsync(StateHasChanged), so neither a render count nor
    // a faulted-subscriber log distinguishes "unsubscribed" from "still wired to a
    // torn-down component" (both were verified against a no-op Dispose).
    [Fact]
    public async Task InboxView_WhenAPublishArrivesAfterDisposal_DoesNotDisturbThePublisher()
    {
        // Arrange
        _holder.Publish(
            [Item("first", "PerfectServe", "repo1")],
            [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]
        );
        Render<InboxView>();

        // Act
        await DisposeComponentsAsync();

        // Assert
        var exception = Record.Exception(() =>
            _holder.Publish(
                [Item("second", "PerfectServe", "repo1")],
                [new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok)]
            )
        );
        Assert.Null(exception);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void InboxView_PlacesRefreshImmediatelyAfterTheReviewInboxHeading()
    {
        // Arrange
        _holder.Publish([], []);

        // Act
        var cut = Render<InboxView>();

        // Assert
        var header = cut.Find("[data-testid=inbox-header]");
        Assert.Equal(["H1", "BUTTON", "DIV"], header.Children.Select(c => c.TagName).ToArray());
        Assert.Equal("Review Inbox", header.Children[0].TextContent.Trim());
    }

    [Fact]
    public void InboxView_NamesItsIconOnlyRefreshForAssistiveTech()
    {
        // Arrange
        _holder.Publish([], []);

        // Act
        var cut = Render<InboxView>();

        // Assert
        Assert.Equal(
            "Refresh the queue",
            cut.Find("[data-testid=refresh]").GetAttribute("aria-label")
        );
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
    public void InboxView_WhileARefreshIsRunning_DisablesRefreshAndSpinsItsIcon()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();

        // Act
        _refreshState.BeginWake();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("[data-testid=refresh]").HasAttribute("disabled"));
            Assert.Contains("icon-spin", cut.Find("[data-testid=refresh] .icon").ClassName);
        });
    }

    [Fact]
    public void InboxView_WhenARefreshCompletes_ReEnablesRefreshAndStopsTheSpin()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();
        _refreshState.BeginWake();

        // Act
        _refreshState.CompleteRefresh(failure: null);

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.False(cut.Find("[data-testid=refresh]").HasAttribute("disabled"));
            Assert.DoesNotContain("icon-spin", cut.Find("[data-testid=refresh] .icon").ClassName);
        });
    }

    [Fact]
    public void InboxView_WhenClickedRepeatedlyBeforeTheRefreshStarts_PokesTheTriggerOnce()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();
        var refresh = cut.Find("[data-testid=refresh]");

        // Act
        refresh.Click();
        refresh.Click();
        refresh.Click();

        // Assert
        _trigger.Received(1).RequestRefresh();
    }

    [Fact]
    public void InboxView_AfterARefreshCompletes_AcceptsTheNextClick()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();
        cut.Find("[data-testid=refresh]").Click();

        // Act
        _refreshState.BeginWake();
        _refreshState.CompleteRefresh(failure: null);
        cut.Find("[data-testid=refresh]").Click();

        // Assert
        _trigger.Received(2).RequestRefresh();
    }

    [Fact]
    public void InboxView_WhenTheRequestedWakePollsNothing_ReEnablesRefresh()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();
        cut.Find("[data-testid=refresh]").Click();

        // Act
        _refreshState.SkipRefresh();

        // Assert
        cut.WaitForAssertion(() =>
            Assert.False(cut.Find("[data-testid=refresh]").HasAttribute("disabled"))
        );
    }

    [Fact]
    public void InboxView_AfterASkippedWake_AcceptsTheNextClick()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();
        cut.Find("[data-testid=refresh]").Click();

        // Act
        _refreshState.SkipRefresh();
        cut.Find("[data-testid=refresh]").Click();

        // Assert
        _trigger.Received(2).RequestRefresh();
    }

    [Fact]
    public void InboxView_WhenAWakeIsSkipped_KeepsTheLastRefreshTimeUnchanged()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();
        _refreshState.BeginWake();
        _refreshState.CompleteRefresh(failure: null);
        var shownBefore = cut.WaitForElement("[data-testid=last-refresh]").TextContent;

        // Act
        _clock.Advance(TimeSpan.FromHours(1));
        _refreshState.SkipRefresh();
        _refreshState.BeginWake();

        // The wake begun after the skip is the barrier: once its disabled state has
        // rendered, the skip's render has landed too, so an unchanged time is a real
        // observation rather than one taken before the skip was ever drawn.
        // Assert
        cut.WaitForAssertion(() =>
            Assert.True(cut.Find("[data-testid=refresh]").HasAttribute("disabled"))
        );
        Assert.Equal(shownBefore, cut.Find("[data-testid=last-refresh]").TextContent);
    }

    [Fact]
    public void InboxView_BeforeAnyRefresh_ShowsNoLastRefreshTime()
    {
        // Arrange
        _holder.Publish([], []);

        // Act
        var cut = Render<InboxView>();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=last-refresh]"));
    }

    [Fact]
    public void InboxView_AfterASuccessfulRefresh_ShowsTheLastRefreshTimeWithoutAFailure()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();

        // Act
        _refreshState.BeginWake();
        _refreshState.CompleteRefresh(failure: null);

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid=last-refresh]"));
            Assert.Empty(cut.FindAll("[data-testid=refresh-failure]"));
        });
    }

    [Fact]
    public void InboxView_AfterAFailedRefresh_ShowsTheFailureAlongsideTheLastRefreshTime()
    {
        // Arrange
        _holder.Publish([], []);
        var cut = Render<InboxView>();

        // Act
        _refreshState.BeginWake();
        _refreshState.CompleteRefresh("The refresh timed out.");

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid=last-refresh]"));
            Assert.Contains(
                "The refresh timed out.",
                cut.Find("[data-testid=refresh-failure]").TextContent
            );
        });
    }

    [Fact]
    public async Task InboxView_WhenARefreshTransitionArrivesAfterDisposal_DoesNotDisturbThePublisher()
    {
        // Arrange
        _holder.Publish([], []);
        Render<InboxView>();

        // Act
        await DisposeComponentsAsync();

        // Assert
        var exception = Record.Exception(() => _refreshState.BeginWake());
        Assert.Null(exception);
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
