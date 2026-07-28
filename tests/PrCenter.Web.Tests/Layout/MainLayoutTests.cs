using Bunit;
using Microsoft.AspNetCore.Components;
using PrCenter.Web.Components.Layout;

namespace PrCenter.Web.Tests.Layout;

public sealed class MainLayoutTests : BunitContext
{
    [Fact]
    public void MainLayout_CarriesNoOutboundChromeLinks()
    {
        // Arrange / Act
        var cut = Render<MainLayout>(ps =>
            ps.Add(p => p.Body, (RenderFragment)(builder => builder.AddContent(0, "body")))
        );

        // Assert
        Assert.Empty(cut.FindAll("a[target=_blank]"));
    }
}
