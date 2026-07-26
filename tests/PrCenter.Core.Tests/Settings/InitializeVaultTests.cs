using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;

namespace PrCenter.Core.Tests.Settings;

public sealed class InitializeVaultTests
{
    private const string Password = "correct horse";

    private readonly ITokenVault _vault = Substitute.For<ITokenVault>();
    private readonly IAppLock _appLock = Substitute.For<IAppLock>();
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();

    [Fact]
    public async Task InitializeAsync_ValidPassword_SetsThePasswordAndUnlocks()
    {
        // Arrange
        _appLock.UnlockAsync(Password, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var unlocked = await CreateUseCase().InitializeAsync(Password, CancellationToken.None);

        // Assert
        Assert.True(unlocked);
        await _vault.Received(1).SetPasswordAsync(Password, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_ValidPassword_PokesTheTrigger()
    {
        // Arrange
        _appLock.UnlockAsync(Password, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await CreateUseCase().InitializeAsync(Password, CancellationToken.None);

        // Assert
        _trigger.Received(1).RequestRefresh();
    }

    [Fact]
    public async Task InitializeAsync_WhenSetPasswordFails_PropagatesAndDoesNotUnlock()
    {
        // Arrange
        _vault
            .SetPasswordAsync(Password, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("The vault is already initialized."));

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateUseCase().InitializeAsync(Password, CancellationToken.None)
        );
        await _appLock.DidNotReceive().UnlockAsync(Password, Arg.Any<CancellationToken>());
        _trigger.DidNotReceive().RequestRefresh();
    }

    [Fact]
    public async Task InitializeAsync_WhenUnlockReturnsFalse_DoesNotPokeTheTrigger()
    {
        // Arrange
        _appLock.UnlockAsync(Password, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var unlocked = await CreateUseCase().InitializeAsync(Password, CancellationToken.None);

        // Assert
        Assert.False(unlocked);
        _trigger.DidNotReceive().RequestRefresh();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InitializeAsync_NullOrWhitespacePassword_Throws(string? password)
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            useCase.InitializeAsync(password!, CancellationToken.None)
        );
    }

    private InitializeVault CreateUseCase() => new(_vault, _appLock, _trigger);
}
