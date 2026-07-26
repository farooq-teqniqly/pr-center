using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;
using PrCenter.Web.Components.Pages;

namespace PrCenter.Web.Tests.Pages;

public sealed class SettingsTests : BunitContext
{
    [Theory]
    [InlineData(AppLockState.Uninitialized, "setup-card")]
    [InlineData(AppLockState.Locked, "unlock-first")]
    [InlineData(AppLockState.Unlocked, "owner-tokens")]
    [InlineData(AppLockState.Unlocked, "poll-interval")]
    public void Settings_ForLockState_RendersTheMatchingView(
        AppLockState state,
        string expectedTestId
    )
    {
        // Arrange
        RegisterLock(state);

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.NotNull(cut.Find($"[data-testid={expectedTestId}]"));
    }

    [Theory]
    [InlineData(AppLockState.Uninitialized, "owner-tokens")]
    [InlineData(AppLockState.Uninitialized, "poll-interval")]
    [InlineData(AppLockState.Locked, "setup-card")]
    [InlineData(AppLockState.Locked, "owner-tokens")]
    [InlineData(AppLockState.Locked, "poll-interval")]
    [InlineData(AppLockState.Locked, "reset-vault")]
    [InlineData(AppLockState.Unlocked, "setup-card")]
    [InlineData(AppLockState.Unlocked, "unlock-first")]
    public void Settings_ForLockState_DoesNotRenderTheOtherViews(
        AppLockState state,
        string absentTestId
    )
    {
        // Arrange
        RegisterLock(state);

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.Empty(cut.FindAll($"[data-testid={absentTestId}]"));
    }

    [Fact]
    public void Settings_WhenSetupCompletes_ReevaluatesTheLockStateAndShowsTheUnlockedView()
    {
        // Arrange
        const string password = "Str0ng-pass!";
        var appLock = Substitute.For<IAppLock>();
        appLock
            .GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(AppLockState.Uninitialized, AppLockState.Unlocked);
        appLock.UnlockAsync(password, Arg.Any<CancellationToken>()).Returns(true);
        Register(appLock);
        var cut = Render<Settings>();

        // Act
        cut.Find("[data-testid=setup-password]").Change(password);
        cut.Find("[data-testid=setup-confirm]").Change(password);
        cut.Find("[data-testid=setup-submit]").Click();

        // Assert
        Assert.NotNull(cut.Find("[data-testid=owner-tokens]"));
        Assert.Empty(cut.FindAll("[data-testid=setup-card]"));
    }

    [Fact]
    public void Settings_WhenLocked_LinksBackToTheInbox()
    {
        // Arrange
        RegisterLock(AppLockState.Locked);

        // Act
        var cut = Render<Settings>();

        // Assert
        Assert.Equal("/", cut.Find("[data-testid=unlock-first] a").GetAttribute("href"));
    }

    private void RegisterLock(AppLockState state)
    {
        var appLock = Substitute.For<IAppLock>();
        appLock.GetStateAsync(Arg.Any<CancellationToken>()).Returns(state);
        Register(appLock);
    }

    private void Register(IAppLock appLock)
    {
        Services.AddLogging();
        Services.AddSingleton(appLock);
        Services.AddSingleton(
            new InitializeVault(
                Substitute.For<ITokenVault>(),
                appLock,
                Substitute.For<IRefreshTrigger>()
            )
        );
    }
}
