using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Web.Components.Locking;

namespace PrCenter.Web.Tests.Locking;

public sealed class UnlockCardTests : BunitContext
{
    private const string Password = "correct horse";
    private const string ConfirmationWord = "RESET";

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
    public void UnlockCard_WhenResetIsInvoked_ShowsTheConfirmationStepAndWipesNothing()
    {
        // Arrange
        var vault = Substitute.For<ITokenVault>();
        var reset = false;
        var cut = RenderCard(Substitute.For<IAppLock>(), vault, onReset: () => reset = true);

        // Act
        cut.Find("[data-testid=reset-vault]").Click();

        // Assert
        var step = cut.Find("[data-testid=reset-confirmation]").TextContent;
        Assert.Contains("app password", step, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token", step, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(vault.ReceivedCalls());
        Assert.False(reset);
    }

    [Fact]
    public void UnlockCard_WithTheConfirmationWord_ResetsTheVaultAndInvokesOnReset()
    {
        // Arrange
        var vault = Substitute.For<ITokenVault>();
        var reset = false;
        var cut = RenderCard(Substitute.For<IAppLock>(), vault, onReset: () => reset = true);

        // Act
        ConfirmReset(cut, ConfirmationWord);

        // Assert
        vault.Received(1).ResetVaultAsync(Arg.Any<CancellationToken>());
        Assert.True(reset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("reset")]
    [InlineData("RESE")]
    [InlineData("RESET ME")]
    public void UnlockCard_WithAMismatchedConfirmationWord_WipesNothingAndStaysOnTheStep(
        string typed
    )
    {
        // Arrange
        var vault = Substitute.For<ITokenVault>();
        var reset = false;
        var cut = RenderCard(Substitute.For<IAppLock>(), vault, onReset: () => reset = true);

        // Act
        ConfirmReset(cut, typed);

        // Assert
        Assert.NotNull(cut.Find("[data-testid=reset-confirmation]"));
        Assert.Empty(vault.ReceivedCalls());
        Assert.False(reset);
    }

    [Fact]
    public void UnlockCard_WhenTheResetIsCancelled_WipesNothingAndReturnsToTheUnlockState()
    {
        // Arrange
        var vault = Substitute.For<ITokenVault>();
        var reset = false;
        var cut = RenderCard(Substitute.For<IAppLock>(), vault, onReset: () => reset = true);
        cut.Find("[data-testid=reset-vault]").Click();

        // Act
        cut.Find("[data-testid=reset-cancel]").Click();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=reset-confirmation]"));
        Assert.NotNull(cut.Find("[data-testid=reset-vault]"));
        Assert.Empty(vault.ReceivedCalls());
        Assert.False(reset);
    }

    [Fact]
    public void UnlockCard_WhenUnlockFailsUnexpectedly_ShowsUnlockFailureAndDoesNotInvokeOnUnlocked()
    {
        // Arrange
        var appLock = Substitute.For<IAppLock>();
        appLock
            .UnlockAsync(Password, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("stored vault data is corrupt"));
        var unlocked = false;
        var cut = RenderCard(appLock, onUnlocked: () => unlocked = true);

        // Act
        Submit(cut, Password);

        // Assert
        Assert.NotNull(cut.Find("[data-testid=unlock-failure]"));
        Assert.False(unlocked);
    }

    private static IAppLock UnlockResult(string password, bool result)
    {
        var appLock = Substitute.For<IAppLock>();
        appLock.UnlockAsync(password, Arg.Any<CancellationToken>()).Returns(result);
        return appLock;
    }

    private static void ConfirmReset(IRenderedComponent<UnlockCard> cut, string typed)
    {
        cut.Find("[data-testid=reset-vault]").Click();
        cut.Find("[data-testid=reset-confirm-word]").Change(typed);
        cut.Find("[data-testid=reset-confirm]").Click();
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
        Services.AddLogging();
        Services.AddSingleton(new UnlockApp(appLock, Substitute.For<IRefreshTrigger>()));
        Services.AddSingleton(vault ?? Substitute.For<ITokenVault>());
        return Render<UnlockCard>(ps =>
            ps.Add(c => c.OnUnlocked, onUnlocked ?? (() => { }))
                .Add(c => c.OnReset, onReset ?? (() => { }))
        );
    }
}
