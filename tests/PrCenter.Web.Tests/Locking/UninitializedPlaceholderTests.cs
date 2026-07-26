using Bunit;
using PrCenter.Web.Components.Locking;

namespace PrCenter.Web.Tests.Locking;

public sealed class UninitializedPlaceholderTests : BunitContext
{
    [Fact]
    public void UninitializedPlaceholder_WhenRendered_LinksToTheSettingsRoute()
    {
        // Arrange / Act
        var cut = Render<UninitializedPlaceholder>();

        // Assert
        Assert.Equal("/settings", cut.Find("[data-testid=uninitialized] a").GetAttribute("href"));
    }
}
