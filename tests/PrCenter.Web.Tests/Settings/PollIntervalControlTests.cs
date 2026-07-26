using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;
using PrCenter.Web.Components.Settings;

namespace PrCenter.Web.Tests.Settings;

public sealed class PollIntervalControlTests : BunitContext
{
    private readonly IAppSettingsStore _store = Substitute.For<IAppSettingsStore>();
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();

    [Fact]
    public void PollIntervalControl_WhenRendered_ShowsTheCurrentIntervalAndTheAllowedRange()
    {
        // Arrange
        StoredInterval(TimeSpan.FromMinutes(30));

        // Act
        var cut = RenderControl();

        // Assert
        Assert.Contains(
            "30",
            cut.Find("[data-testid=current-interval]").TextContent,
            StringComparison.Ordinal
        );
        var range = cut.Find("[data-testid=interval-range]").TextContent;
        Assert.Contains("5 minutes", range, StringComparison.Ordinal);
        Assert.Contains("24 hours", range, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(45)]
    [InlineData(1440)]
    public void PollIntervalControl_WithAnInRangeInterval_StoresIt(int minutes)
    {
        // Arrange
        StoredInterval(TimeSpan.FromMinutes(30));
        var cut = RenderControl();

        // Act
        Submit(cut, minutes);

        // Assert
        _store
            .Received(1)
            .SetPollIntervalAsync(
                new PollInterval(TimeSpan.FromMinutes(minutes)),
                Arg.Any<CancellationToken>()
            );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(1441)]
    public void PollIntervalControl_WithAnOutOfRangeInterval_ShowsTheRangeMessageAndStoresNothing(
        int minutes
    )
    {
        // Arrange
        StoredInterval(TimeSpan.FromMinutes(30));
        var cut = RenderControl();

        // Act
        Submit(cut, minutes);

        // Assert
        var message = cut.Find("[data-testid=interval-error]").TextContent;
        Assert.Contains("5 minutes", message, StringComparison.Ordinal);
        Assert.Contains("24 hours", message, StringComparison.Ordinal);
        _store
            .DidNotReceive()
            .SetPollIntervalAsync(Arg.Any<PollInterval>(), Arg.Any<CancellationToken>());
    }

    private static void Submit(IRenderedComponent<PollIntervalControl> cut, int minutes)
    {
        cut.Find("[data-testid=interval-minutes]")
            .Change(minutes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cut.Find("[data-testid=save-interval]").Click();
    }

    private void StoredInterval(TimeSpan interval) =>
        _store
            .GetPollIntervalAsync(Arg.Any<CancellationToken>())
            .Returns(new PollInterval(interval));

    private IRenderedComponent<PollIntervalControl> RenderControl()
    {
        Services.AddSingleton(_store);
        Services.AddSingleton(new SavePollInterval(_store, _trigger));
        return Render<PollIntervalControl>();
    }
}
