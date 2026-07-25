using Bunit;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Web.Components.Queue;

namespace PrCenter.Web.Tests.Queue;

public sealed class ErrorBannerTests : BunitContext
{
    [Fact]
    public void ErrorBanner_WhenAllOwnersOk_RendersNoBanner()
    {
        // Arrange
        IReadOnlyList<OwnerStatus> statuses =
        [
            new OwnerStatus("PerfectServe", OwnerFetchStatus.Ok),
        ];

        // Act
        var cut = Render<ErrorBanner>(ps => ps.Add(p => p.OwnerStatuses, statuses));

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=error-banner]"));
    }

    [Fact]
    public void ErrorBanner_ForNonOkOwner_RendersLabeledBanner()
    {
        // Arrange
        IReadOnlyList<OwnerStatus> statuses =
        [
            new OwnerStatus(
                "ps-unite",
                OwnerFetchStatus.MisconfiguredToken,
                "token not SSO-authorized"
            ),
        ];

        // Act
        var cut = Render<ErrorBanner>(ps => ps.Add(p => p.OwnerStatuses, statuses));
        var banner = cut.Find("[data-testid=error-banner]");

        // Assert
        Assert.Contains("ps-unite", banner.TextContent, StringComparison.Ordinal);
        Assert.Contains("token not SSO-authorized", banner.TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OwnerFetchStatus.MisconfiguredToken)]
    [InlineData(OwnerFetchStatus.Error)]
    public void ErrorBanner_WhenTheStatusCarriesNoDetail_ExplainsTheFailureWithoutNamingTheEnum(
        OwnerFetchStatus status
    )
    {
        // Arrange
        IReadOnlyList<OwnerStatus> statuses = [new OwnerStatus("ps-unite", status)];

        // Act
        var cut = Render<ErrorBanner>(ps => ps.Add(p => p.OwnerStatuses, statuses));
        var banner = cut.Find("[data-testid=error-banner]");

        // Assert
        Assert.DoesNotContain(status.ToString(), banner.TextContent, StringComparison.Ordinal);
        Assert.Contains("ps-unite", banner.TextContent, StringComparison.Ordinal);
        Assert.EndsWith(".", banner.TextContent.Trim(), StringComparison.Ordinal);
    }
}
