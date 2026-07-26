using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;

namespace PrCenter.Core.Tests.Settings;

public sealed class SaveOwnerTokenTests
{
    private const string Owner = "PerfectServe";
    private const string Token = "github_pat_abc";

    private readonly ITokenVault _vault = Substitute.For<ITokenVault>();
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();

    [Fact]
    public async Task SaveAsync_ValidOwnerAndToken_StoresTheToken()
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act
        await useCase.SaveAsync(Owner, Token, CancellationToken.None);

        // Assert
        await _vault.Received(1).StoreTokenAsync(Owner, Token, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_ValidOwnerAndToken_PokesTheTrigger()
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act
        await useCase.SaveAsync(Owner, Token, CancellationToken.None);

        // Assert
        _trigger.Received(1).RequestRefresh();
    }

    [Fact]
    public async Task SaveAsync_WhenTheVaultThrows_PropagatesAndDoesNotPokeTheTrigger()
    {
        // Arrange
        _vault
            .StoreTokenAsync(Owner, Token, Arg.Any<CancellationToken>())
            .ThrowsAsync(new VaultLockedException());

        // Act / Assert
        await Assert.ThrowsAsync<VaultLockedException>(() =>
            CreateUseCase().SaveAsync(Owner, Token, CancellationToken.None)
        );
        _trigger.DidNotReceive().RequestRefresh();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_NullOrWhitespaceOwner_Throws(string? owner)
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            useCase.SaveAsync(owner!, Token, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_NullOrWhitespaceToken_Throws(string? token)
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            useCase.SaveAsync(Owner, token!, CancellationToken.None)
        );
    }

    private SaveOwnerToken CreateUseCase() => new(_vault, _trigger);
}
