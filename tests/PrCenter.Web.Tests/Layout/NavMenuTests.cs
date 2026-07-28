using Bunit;
using PrCenter.Web.Components.Layout;

namespace PrCenter.Web.Tests.Layout;

public sealed class NavMenuTests : BunitContext
{
    [Fact]
    public void NavMenu_BrandsTheAppAsPrCenter()
    {
        // Arrange / Act
        var cut = Render<NavMenu>();

        // Assert
        Assert.Equal("PR-Center", cut.Find(".navbar-brand").TextContent.Trim());
    }

    [Theory]
    [InlineData("Inbox", "")]
    [InlineData("Settings", "settings")]
    public void NavMenu_LinksEachDestinationToItsRoute(string label, string href)
    {
        // Arrange / Act
        var cut = Render<NavMenu>();

        // Assert
        var link = cut.FindAll("nav a").Single(a => a.TextContent.Trim() == label);
        Assert.Equal(href, link.GetAttribute("href"));
    }
}
