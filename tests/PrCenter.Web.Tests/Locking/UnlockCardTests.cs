using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Web.Components.Locking;

namespace PrCenter.Web.Tests.Locking;

public sealed class UnlockCardTests : BunitContext
{
    private const string Password = "correct horse";

    [Fact]
    public void UnlockCard_WithCorrectPassword_InvokesOnUnlocked()
    {
        // Arrange
        var unlocked = false;
        var cut = RenderCard(UnlockResult(Password, true), onUnlocked: () => unlocked = true);

        // Act
        Submit(cut, Password);

        // Assert
        Assert.True(unlocked);
    }

    [Fact]
    public void UnlockCard_WithWrongPassword_ShowsErrorAndDoesNotInvokeOnUnlocked()
    {
        // Arrange
        var unlocked = false;
        var cut = RenderCard(UnlockResult("wrong", false), onUnlocked: () => unlocked = true);

        // Act
        Submit(cut, "wrong");

        // Assert
        Assert.NotNull(cut.Find("[data-testid=unlock-error]"));
        Assert.False(unlocked);
    }

    [Fact]
    public void UnlockCard_WhenResetInvoked_ResetsTheVaultAndInvokesOnReset()
    {
        // Arrange
        var vault = Substitute.For<ITokenVault>();
        var reset = false;
        var cut = RenderCard(Substitute.For<IAppLock>(), vault, onReset: () => reset = true);

        // Act
        cut.Find("[data-testid=reset-vault]").Click();

        // Assert
        vault.Received(1).ResetVaultAsync(Arg.Any<CancellationToken>());
        Assert.True(reset);
    }

    private static IAppLock UnlockResult(string password, bool result)
    {
        var appLock = Substitute.For<IAppLock>();
        appLock.UnlockAsync(password, Arg.Any<CancellationToken>()).Returns(result);
        return appLock;
    }

    private static void Submit(IRenderedComponent<UnlockCard> cut, string password)
    {
        cut.Find("[data-testid=password]").Change(password);
        cut.Find("[data-testid=unlock-submit]").Click();
    }

    private IRenderedComponent<UnlockCard> RenderCard(
        IAppLock appLock,
        ITokenVault? vault = null,
        Action? onUnlocked = null,
        Action? onReset = null
    )
    {
        Services.AddSingleton(new UnlockApp(appLock, Substitute.For<IRefreshTrigger>()));
        Services.AddSingleton(vault ?? Substitute.For<ITokenVault>());
        return Render<UnlockCard>(ps =>
            ps.Add(c => c.OnUnlocked, onUnlocked ?? (() => { }))
                .Add(c => c.OnReset, onReset ?? (() => { }))
        );
    }
}
