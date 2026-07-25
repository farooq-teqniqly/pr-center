using Bunit;
using PrCenter.Core.Derivation;
using PrCenter.Web.Components.Queue;

namespace PrCenter.Web.Tests.Queue;

public sealed class RosterChipsTests : BunitContext
{
    [Theory]
    [InlineData(ReviewerState.Approved, "state-approved", "approved")]
    [InlineData(ReviewerState.ChangesRequested, "state-changes", "changes requested")]
    [InlineData(ReviewerState.Commented, "state-commented", "commented")]
    [InlineData(ReviewerState.Pending, "state-pending", "requested")]
    public void RosterChips_ForReviewerState_ColorsAndLabelsTheChip(
        ReviewerState state,
        string expectedClass,
        string expectedLabel
    )
    {
        // Arrange
        IReadOnlyList<ReviewerRosterEntry> roster =
        [
            new("octocat", state, isBot: false, isMe: false),
        ];

        // Act
        var cut = Render<RosterChips>(ps => ps.Add(p => p.Roster, roster));
        var chip = cut.Find("[data-testid=roster-chip]");

        // Assert
        Assert.Contains(expectedClass, chip.ClassList);
        Assert.Contains(expectedLabel, chip.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void RosterChips_ForMe_ShowsDashedRingAndMeLabel()
    {
        // Arrange
        IReadOnlyList<ReviewerRosterEntry> roster =
        [
            new("octocat", ReviewerState.Commented, isBot: false, isMe: true),
        ];

        // Act
        var cut = Render<RosterChips>(ps => ps.Add(p => p.Roster, roster));
        var chip = cut.Find("[data-testid=roster-chip]");

        // Assert
        Assert.Contains("chip-me", chip.ClassList);
        Assert.Contains("me", chip.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void RosterChips_ForBot_ShowsBotTreatmentAsTextNotColorOnly()
    {
        // Arrange
        IReadOnlyList<ReviewerRosterEntry> roster =
        [
            new("qodo-merge[bot]", ReviewerState.Commented, isBot: true, isMe: false),
        ];

        // Act
        var cut = Render<RosterChips>(ps => ps.Add(p => p.Roster, roster));
        var chip = cut.Find("[data-testid=roster-chip]");

        // Assert
        Assert.Contains("chip-bot", chip.ClassList);
        Assert.Contains("bot", chip.TextContent, StringComparison.Ordinal);
    }
}
