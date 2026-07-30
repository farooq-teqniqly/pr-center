using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;
using PrCenter.Web.Components.Settings;

namespace PrCenter.Web.Tests.Settings;

public sealed class PollDiagnosticsViewTests : BunitContext
{
    private readonly IPollDiagnosticsReader _reader = Substitute.For<IPollDiagnosticsReader>();

    [Fact]
    public void PollDiagnosticsView_RendersPollsNewestFirst()
    {
        // Arrange -- the reader already returns newest first; the view preserves it
        var newest = DiagnosticsRecords.Poll(DiagnosticsRecords.At);
        var older = DiagnosticsRecords.Poll(DiagnosticsRecords.At.AddMinutes(-5));
        Returns(newest, older);

        // Act
        var cut = RenderView();

        // Assert
        Assert.Equal(
            [newest.Run.PollId.ToString(), older.Run.PollId.ToString()],
            cut.FindAll("[data-testid=poll-summary]")
                .Select(row => row.GetAttribute("data-poll-id"))
        );
    }

    [Fact]
    public void PollDiagnosticsView_SummaryLine_CarriesInstantOutcomeRatioAndPublishedCount()
    {
        // Arrange
        Returns(DiagnosticsRecords.Poll(configuredOwners: ["acme", "ps-unite"]));

        // Act
        var cut = RenderView();

        // Assert -- shown in the reader's own timezone, as the inbox does
        Assert.Equal(
            DiagnosticsRecords.At.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            Text(cut, "poll-instant")
        );
        Assert.Equal("ok", Text(cut, "poll-outcome"));
        Assert.Equal("2/2 owners", Text(cut, "poll-owner-ratio"));
        Assert.Equal("published 12", Text(cut, "poll-published"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenFewerOwnersWerePolledThanConfigured_ShowsTheShortfall()
    {
        // Arrange
        Returns(
            DiagnosticsRecords.Poll(
                outcome: PollOutcome.AbortedByLock,
                publishedCount: null,
                configuredOwners: ["acme", "ps-unite", "farooq"],
                owners:
                [
                    DiagnosticsRecords.Polled("acme"),
                    DiagnosticsRecords.Unreached("ps-unite"),
                    DiagnosticsRecords.Unreached("farooq"),
                ]
            )
        );

        // Act
        var cut = RenderView();

        // Assert -- readable without expanding the poll
        Assert.Equal("1/3 owners", Text(cut, "poll-owner-ratio"));
        Assert.Empty(cut.FindAll("[data-testid=diagnostics-owner-row]"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenTheOwnerCountIsAbsent_RendersTheRatioAsUnknown()
    {
        // Arrange
        Returns(DiagnosticsRecords.WithoutConfiguredOwners());

        // Act
        var cut = RenderView();

        // Assert -- an unreadable owner list must not read as zero owners configured
        Assert.Equal("-/- owners", Text(cut, "poll-owner-ratio"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenNothingWasPublished_RendersTheCountAsAbsent()
    {
        // Arrange
        Returns(DiagnosticsRecords.Poll(outcome: PollOutcome.Faulted, publishedCount: null));

        // Act
        var cut = RenderView();

        // Assert
        Assert.Equal("published --", Text(cut, "poll-published"));
    }

    [Fact]
    public void PollDiagnosticsView_OnFirstRender_CollapsesTheOwnerRows()
    {
        // Arrange
        Returns(DiagnosticsRecords.Poll());

        // Act
        var cut = RenderView();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=diagnostics-owner-row]"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenAPollIsExpanded_RevealsItsOwnerRows()
    {
        // Arrange
        Returns(DiagnosticsRecords.Poll(configuredOwners: ["acme", "ps-unite"]));
        var cut = RenderView();

        // Act
        cut.Find("[data-testid=poll-toggle]").Click();

        // Assert
        Assert.Equal(
            ["acme", "ps-unite"],
            cut.FindAll("[data-testid=diagnostics-owner-row]")
                .Select(row => row.GetAttribute("data-owner"))
        );
    }

    [Fact]
    public async Task PollDiagnosticsView_WhenAPollIsExpanded_IssuesNoFurtherRead()
    {
        // Arrange
        Returns(DiagnosticsRecords.Poll());
        var cut = RenderView();

        // Act
        await cut.Find("[data-testid=poll-toggle]").ClickAsync(new MouseEventArgs());

        // Assert -- the whole graph came back on the one read; expanding is display only
        await _reader.Received(1).GetRecentPollsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(PollOutcome.Succeeded, "ok")]
    [InlineData(PollOutcome.AbortedByLock, "aborted")]
    [InlineData(PollOutcome.Canceled, "incomplete")]
    [InlineData(PollOutcome.Faulted, "failed")]
    public void PollDiagnosticsView_ForEachOutcome_RendersItsLabel(
        PollOutcome outcome,
        string expected
    )
    {
        // Arrange -- a canceled poll never finished; that is not the same as failing
        Returns(DiagnosticsRecords.Poll(outcome: outcome));

        // Act
        var cut = RenderView();

        // Assert
        Assert.Equal(expected, Text(cut, "poll-outcome"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenAnOwnerWasNeverPolled_RendersItAsNotPolledRatherThanZero()
    {
        // Arrange
        Returns(
            DiagnosticsRecords.Poll(
                outcome: PollOutcome.AbortedByLock,
                publishedCount: null,
                configuredOwners: ["acme"],
                owners: [DiagnosticsRecords.Unreached("acme")]
            )
        );
        var cut = RenderView();

        // Act
        cut.Find("[data-testid=poll-toggle]").Click();

        // Assert
        Assert.Equal("not polled", Text(cut, "owner-status"));
        Assert.DoesNotContain("0", Text(cut, "owner-counts"), StringComparison.Ordinal);
    }

    [Fact]
    public void PollDiagnosticsView_WhenAnOwnerCarriedRowsOver_ShowsTheCarryOverCount()
    {
        // Arrange
        Returns(
            DiagnosticsRecords.Poll(
                configuredOwners: ["acme"],
                owners: [DiagnosticsRecords.Failed("acme", carriedOver: 5)]
            )
        );
        var cut = RenderView();

        // Act
        cut.Find("[data-testid=poll-toggle]").Click();

        // Assert
        Assert.Contains("carried 5", Text(cut, "owner-counts"), StringComparison.Ordinal);
    }

    [Fact]
    public void PollDiagnosticsView_WhenAnOwnerReachedIntoAnother_ShowsItsForeignItemCount()
    {
        // Arrange
        Returns(
            DiagnosticsRecords.Poll(
                configuredOwners: ["acme"],
                owners:
                [
                    DiagnosticsRecords.Polled(
                        "acme",
                        foreignCount: 2,
                        ids: ["acme/api#12", "ps-unite/tools#3", "farooq/pr-center#42"]
                    ),
                ]
            )
        );
        var cut = RenderView();

        // Act
        cut.Find("[data-testid=poll-toggle]").Click();

        // Assert
        Assert.Contains("foreign 2", Text(cut, "owner-foreign"), StringComparison.Ordinal);
    }

    [Fact]
    public void PollDiagnosticsView_WithAnEmptyStore_RendersAnEmptyState()
    {
        // Arrange
        Returns();

        // Act
        var cut = RenderView();

        // Assert -- distinguishable from a failed read, which is a different message
        Assert.NotNull(cut.Find("[data-testid=diagnostics-empty]"));
        Assert.Empty(cut.FindAll("[data-testid=diagnostics-error]"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenTheReadFails_RendersAnErrorDistinctFromTheEmptyState()
    {
        // Arrange
        _reader
            .GetRecentPollsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("the diagnostics table is unreadable"));

        // Act
        var cut = RenderView();

        // Assert
        Assert.NotNull(cut.Find("[data-testid=diagnostics-error]"));
        Assert.Empty(cut.FindAll("[data-testid=diagnostics-empty]"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenAConfiguredOwnerHasNoRow_SurfacesTheDisagreementAndNamesIt()
    {
        // Arrange
        Returns(
            DiagnosticsRecords.Poll(
                configuredOwners: ["acme", "ps-unite"],
                owners: [DiagnosticsRecords.Polled("acme")]
            )
        );

        // Act
        var cut = RenderView();

        // Assert
        Assert.Contains("ps-unite", Text(cut, "poll-disagreement"), StringComparison.Ordinal);
    }

    [Fact]
    public void PollDiagnosticsView_WhenARowNamesAnUnconfiguredOwner_SurfacesTheDisagreement()
    {
        // Arrange
        Returns(
            DiagnosticsRecords.Poll(
                configuredOwners: ["acme"],
                owners: [DiagnosticsRecords.Polled("acme"), DiagnosticsRecords.Polled("stranger")]
            )
        );

        // Act
        var cut = RenderView();

        // Assert
        Assert.Contains("stranger", Text(cut, "poll-disagreement"), StringComparison.Ordinal);
    }

    [Fact]
    public void PollDiagnosticsView_WhenTheOwnersAgree_CarriesNoDisagreement()
    {
        // Arrange
        Returns(DiagnosticsRecords.Poll(configuredOwners: ["acme", "ps-unite"]));

        // Act
        var cut = RenderView();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=poll-disagreement]"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenConfiguredOwnersAreAbsent_CarriesNoDisagreement()
    {
        // Arrange -- nothing to disagree with: the enumeration never completed
        Returns(DiagnosticsRecords.WithoutConfiguredOwners());

        // Act
        var cut = RenderView();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=poll-disagreement]"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenOwnerRowsSumAbovePublished_MarksTheOverlapWithBothTotals()
    {
        // Arrange -- both owners derived the same pull request, published once
        Returns(
            DiagnosticsRecords.Poll(
                publishedCount: 1,
                configuredOwners: ["acme", "ps-unite"],
                owners:
                [
                    DiagnosticsRecords.Polled("acme", derived: 1),
                    DiagnosticsRecords.Polled("ps-unite", derived: 1),
                ]
            )
        );

        // Act
        var cut = RenderView();

        // Assert
        var mark = Text(cut, "poll-overlap");
        Assert.Contains("2", mark, StringComparison.Ordinal);
        Assert.Contains("1", mark, StringComparison.Ordinal);
    }

    [Fact]
    public void PollDiagnosticsView_WhenTheCountsAddUp_CarriesNoOverlapMark()
    {
        // Arrange
        Returns(
            DiagnosticsRecords.Poll(
                publishedCount: 12,
                configuredOwners: ["acme"],
                owners: [DiagnosticsRecords.Polled("acme", derived: 12)]
            )
        );

        // Act
        var cut = RenderView();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=poll-overlap]"));
    }

    [Fact]
    public void PollDiagnosticsView_WhenOverlapIsMarked_StillRendersTheOutcomeAsSuccessful()
    {
        // Arrange -- cross-owner overlap is legitimate, so the mark is not an alarm
        Returns(
            DiagnosticsRecords.Poll(
                publishedCount: 1,
                configuredOwners: ["acme", "ps-unite"],
                owners:
                [
                    DiagnosticsRecords.Polled("acme", derived: 1),
                    DiagnosticsRecords.Polled("ps-unite", derived: 1),
                ]
            )
        );

        // Act
        var cut = RenderView();

        // Assert
        Assert.Equal("ok", Text(cut, "poll-outcome"));
        var mark = cut.Find("[data-testid=poll-overlap]").GetAttribute("class") ?? string.Empty;
        Assert.DoesNotContain("danger", mark, StringComparison.Ordinal);
        Assert.DoesNotContain("warning", mark, StringComparison.Ordinal);
    }

    [Fact]
    public void PollDiagnosticsView_CopyAction_ProducesIdentifiersCountsStatusesAndInstants()
    {
        // Arrange
        JSInterop.Mode = JSRuntimeMode.Loose;
        Returns(
            DiagnosticsRecords.Poll(
                configuredOwners: ["acme"],
                owners: [DiagnosticsRecords.Polled("acme", derived: 4, ids: ["acme/api#12"])]
            )
        );
        var cut = RenderView();

        // Act
        cut.Find("[data-testid=copy-diagnostics]").Click();

        // Assert
        var copied = CopiedText();
        Assert.Contains("acme/api#12", copied, StringComparison.Ordinal);
        Assert.Contains("derived 4", copied, StringComparison.Ordinal);
        Assert.Contains("Ok", copied, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-07-29T14:05:12", copied, StringComparison.Ordinal);
    }

    [Fact]
    public void PollDiagnosticsView_CopyAction_CarriesNoGitHubPayloadBeyondIdentifiers()
    {
        // Arrange
        JSInterop.Mode = JSRuntimeMode.Loose;
        Returns(DiagnosticsRecords.Poll());
        var cut = RenderView();

        // Act
        cut.Find("[data-testid=copy-diagnostics]").Click();

        // Assert -- no URL, and nothing that could carry a title or body
        var copied = CopiedText();
        Assert.DoesNotContain("https://", copied, StringComparison.Ordinal);
        Assert.DoesNotContain("github.com", copied, StringComparison.Ordinal);
    }

    private string CopiedText() =>
        JSInterop.Invocations["navigator.clipboard.writeText"].Single().Arguments[0]!.ToString()!;

    private void Returns(params PollDiagnostics[] polls) =>
        _reader.GetRecentPollsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(polls);

    private IRenderedComponent<PollDiagnosticsView> RenderView()
    {
        Services.AddLogging();
        Services.AddSingleton(_reader);
        return Render<PollDiagnosticsView>();
    }

    private static string Text(IRenderedComponent<PollDiagnosticsView> cut, string testId) =>
        cut.Find($"[data-testid={testId}]").TextContent.Trim();
}
