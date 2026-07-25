using Bunit;
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

    private void RegisterLock(AppLockState state)
    {
        var appLock = Substitute.For<IAppLock>();
        appLock.GetStateAsync(Arg.Any<CancellationToken>()).Returns(state);
        Services.AddSingleton(appLock);
        Services.AddSingleton(new UnlockApp(appLock, Substitute.For<IRefreshTrigger>()));
        Services.AddSingleton(Substitute.For<ITokenVault>());
    }
}
