using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;

namespace PrCenter.Core.Tests.Settings;

public sealed class RemoveOwnerTests
{
    private const string Owner = "PerfectServe";

    private readonly ITokenVault _vault = Substitute.For<ITokenVault>();
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();

    [Fact]
    public async Task RemoveAsync_ValidOwner_DeletesTheToken()
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act
        await useCase.RemoveAsync(Owner, CancellationToken.None);

        // Assert
        await _vault.Received(1).DeleteTokenAsync(Owner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_ValidOwner_PokesTheTrigger()
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act
        await useCase.RemoveAsync(Owner, CancellationToken.None);

        // Assert
        _trigger.Received(1).RequestRefresh();
    }

    [Fact]
    public async Task RemoveAsync_WhenTheVaultThrows_PropagatesAndDoesNotPokeTheTrigger()
    {
        // Arrange
        _vault
            .DeleteTokenAsync(Owner, Arg.Any<CancellationToken>())
            .ThrowsAsync(new VaultLockedException());

        // Act / Assert
        await Assert.ThrowsAsync<VaultLockedException>(() =>
            CreateUseCase().RemoveAsync(Owner, CancellationToken.None)
        );
        _trigger.DidNotReceive().RequestRefresh();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveAsync_NullOrWhitespaceOwner_Throws(string? owner)
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            useCase.RemoveAsync(owner!, CancellationToken.None)
        );
    }

    private RemoveOwner CreateUseCase() => new(_vault, _trigger);
}
