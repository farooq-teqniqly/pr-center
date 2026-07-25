using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Web.Components.Queue;

namespace PrCenter.Web.Tests.Queue;

public sealed class OwnerChipsTests : BunitContext
{
    public OwnerChipsTests() => Services.AddSingleton(TimeProvider.System);

    [Fact]
    public void OwnerChips_ForOkOwner_RendersOkChip()
    {
        // Arrange
        IReadOnlyList<OwnerStatus> statuses =
        [
            new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok),
        ];

        // Act
        var cut = Render<OwnerChips>(ps => ps.Add(p => p.OwnerStatuses, statuses));
        var chip = cut.Find("[data-testid=owner-chip]");

        // Assert
        Assert.Contains("status-ok", chip.ClassList);
        Assert.Contains("ok", chip.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerChips_ForNonOkOwner_RendersStaleChipWithLastFreshAt()
    {
        // Arrange
        var lastFreshAt = new DateTimeOffset(2026, 7, 14, 7, 0, 0, TimeSpan.Zero);
        IReadOnlyList<OwnerStatus> statuses =
        [
            new OwnerStatus("ps-unite", OwnerFetchStatus.Error, "token rejected", lastFreshAt),
        ];

        // Act
        var cut = Render<OwnerChips>(ps => ps.Add(p => p.OwnerStatuses, statuses));
        var chip = cut.Find("[data-testid=owner-chip]");

        // Assert
        Assert.Contains("status-err", chip.ClassList);
        Assert.Contains("stale", chip.TextContent, StringComparison.Ordinal);
    }
}
