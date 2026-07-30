using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;
using SettingsPage = PrCenter.Web.Components.Pages.Settings;

namespace PrCenter.Web.Tests.Pages;

public sealed class SettingsTests : BunitContext
{
    [Theory]
    [InlineData(AppLockState.Uninitialized, "setup-card")]
    [InlineData(AppLockState.Locked, "unlock-first")]
    [InlineData(AppLockState.Unlocked, "owner-tokens")]
    [InlineData(AppLockState.Unlocked, "poll-interval")]
    [InlineData(AppLockState.Unlocked, "poll-diagnostics")]
    public void Settings_ForLockState_RendersTheMatchingView(
        AppLockState state,
        string expectedTestId
    )
    {
        // Arrange
        RegisterLock(state);

        // Act
        var cut = Render<SettingsPage>();

        // Assert
        Assert.NotNull(cut.Find($"[data-testid={expectedTestId}]"));
    }

    [Theory]
    [InlineData(AppLockState.Uninitialized, "owner-tokens")]
    [InlineData(AppLockState.Uninitialized, "poll-interval")]
    [InlineData(AppLockState.Uninitialized, "poll-diagnostics")]
    [InlineData(AppLockState.Locked, "setup-card")]
    [InlineData(AppLockState.Locked, "owner-tokens")]
    [InlineData(AppLockState.Locked, "poll-interval")]
    [InlineData(AppLockState.Locked, "poll-diagnostics")]
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
        var cut = Render<SettingsPage>();

        // Assert
        Assert.Empty(cut.FindAll($"[data-testid={absentTestId}]"));
    }

    [Fact]
    public void Settings_WhenSetupCompletes_ReevaluatesTheLockStateAndShowsTheUnlockedView()
    {
        // Arrange
        const string password = "example-pass-9!";
        var appLock = Substitute.For<IAppLock>();
        appLock
            .GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(AppLockState.Uninitialized, AppLockState.Unlocked);
        appLock.UnlockAsync(password, Arg.Any<CancellationToken>()).Returns(true);
        Register(appLock);
        var cut = Render<SettingsPage>();

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
        var cut = Render<SettingsPage>();

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
        var vault = Substitute.For<ITokenVault>();
        var trigger = Substitute.For<IRefreshTrigger>();
        var diagnostics = Substitute.For<IPollDiagnosticsReader>();
        diagnostics.GetRecentPollsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        var settingsStore = Substitute.For<IAppSettingsStore>();
        settingsStore
            .GetPollIntervalAsync(Arg.Any<CancellationToken>())
            .Returns(PollInterval.Default);
        var holder = new QueueSnapshotHolder(
            TimeProvider.System,
            NullLogger<QueueSnapshotHolder>.Instance
        );

        Services.AddLogging();
        Services.AddSingleton(appLock);
        Services.AddSingleton(vault);
        Services.AddSingleton(holder);
        Services.AddSingleton(new GetQueue(holder));
        Services.AddSingleton(new InitializeVault(vault, appLock, trigger));
        Services.AddSingleton(new SaveOwnerToken(vault, trigger));
        Services.AddSingleton(new RemoveOwner(vault, trigger));
        Services.AddSingleton(settingsStore);
        Services.AddSingleton(diagnostics);
        Services.AddSingleton(new SavePollInterval(settingsStore, trigger));
    }
}
