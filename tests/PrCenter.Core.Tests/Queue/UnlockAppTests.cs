namespace PrCenter.Core.Tests.Queue;

using NSubstitute;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

public sealed class UnlockAppTests
{
    private const string Password = "correct horse";

    private readonly IAppLock _appLock = Substitute.For<IAppLock>();
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();

    [Fact]
    public async Task UnlockAsync_WhenUnlockSucceeds_PokesTheTriggerAndReturnsTrue()
    {
        // Arrange
        _appLock.UnlockAsync(Password, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var unlocked = await CreateUnlockApp().UnlockAsync(Password, CancellationToken.None);

        // Assert
        Assert.True(unlocked);
        _trigger.Received(1).RequestRefresh();
    }

    [Fact]
    public async Task UnlockAsync_WhenUnlockFails_DoesNotPokeTheTriggerAndReturnsFalse()
    {
        // Arrange
        _appLock.UnlockAsync(Password, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var unlocked = await CreateUnlockApp().UnlockAsync(Password, CancellationToken.None);

        // Assert
        Assert.False(unlocked);
        _trigger.DidNotReceive().RequestRefresh();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnlockAsync_NullOrWhitespacePassword_Throws(string? password)
    {
        // Arrange
        var unlockApp = CreateUnlockApp();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            unlockApp.UnlockAsync(password!, CancellationToken.None)
        );
    }

    private UnlockApp CreateUnlockApp() => new(_appLock, _trigger);
}
