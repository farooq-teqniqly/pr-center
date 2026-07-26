using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Web.Components.Locking;

namespace PrCenter.Web.Tests.Locking;

public sealed class LockGateTests : BunitContext
{
    [Theory]
    [InlineData(AppLockState.Unlocked, "inbox")]
    [InlineData(AppLockState.Locked, "unlock-card")]
    [InlineData(AppLockState.Uninitialized, "uninitialized")]
    public void LockGate_ForLockState_RendersTheMatchingScreen(
        AppLockState state,
        string expectedTestId
    )
    {
        // Arrange
        RegisterLock(state);

        // Act
        var cut = Render<LockGate>(ps => ps.AddChildContent("<p data-testid=\"inbox\">INBOX</p>"));

        // Assert
        Assert.NotNull(cut.Find($"[data-testid={expectedTestId}]"));
    }

    [Theory]
    [InlineData(AppLockState.Locked, "supplied-locked")]
    [InlineData(AppLockState.Uninitialized, "supplied-uninitialized")]
    public void LockGate_WhenTheCallerSuppliesAScreen_RendersThatScreenForItsState(
        AppLockState state,
        string expectedTestId
    )
    {
        // Arrange
        RegisterLock(state);

        // Act
        var cut = Render<LockGate>(WithSuppliedScreens);

        // Assert
        Assert.NotNull(cut.Find($"[data-testid={expectedTestId}]"));
    }

    [Theory]
    [InlineData(AppLockState.Locked, "unlock-card")]
    [InlineData(AppLockState.Uninitialized, "uninitialized")]
    public void LockGate_WhenTheCallerSuppliesAScreen_DoesNotRenderTheDefaultOne(
        AppLockState state,
        string defaultTestId
    )
    {
        // Arrange
        RegisterLock(state);

        // Act
        var cut = Render<LockGate>(WithSuppliedScreens);

        // Assert
        Assert.Empty(cut.FindAll($"[data-testid={defaultTestId}]"));
    }

    [Fact]
    public void LockGate_WhenUnlockedAndScreensAreSupplied_StillRendersChildContent()
    {
        // Arrange
        RegisterLock(AppLockState.Unlocked);

        // Act
        var cut = Render<LockGate>(WithSuppliedScreens);

        // Assert
        Assert.NotNull(cut.Find("[data-testid=inbox]"));
    }

    private static void WithSuppliedScreens(ComponentParameterCollectionBuilder<LockGate> ps) =>
        ps.AddChildContent("<p data-testid=\"inbox\">INBOX</p>")
            .Add(gate => gate.Locked, _ => "<p data-testid=\"supplied-locked\">UNLOCK FIRST</p>")
            .Add(
                gate => gate.Uninitialized,
                _ => "<p data-testid=\"supplied-uninitialized\">SET UP</p>"
            );

    private void RegisterLock(AppLockState state)
    {
        var appLock = Substitute.For<IAppLock>();
        appLock.GetStateAsync(Arg.Any<CancellationToken>()).Returns(state);
        Services.AddSingleton(appLock);
        Services.AddSingleton(new UnlockApp(appLock, Substitute.For<IRefreshTrigger>()));
        Services.AddSingleton(Substitute.For<ITokenVault>());
    }
}
