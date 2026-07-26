using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;

namespace PrCenter.Core.Tests.Settings;

public sealed class SavePollIntervalTests
{
    private static readonly PollInterval Interval = new(TimeSpan.FromMinutes(15));

    private readonly IAppSettingsStore _store = Substitute.For<IAppSettingsStore>();
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();

    [Fact]
    public async Task SaveAsync_InRangeInterval_StoresIt()
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act
        await useCase.SaveAsync(Interval, CancellationToken.None);

        // Assert
        await _store.Received(1).SetPollIntervalAsync(Interval, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_InRangeInterval_PokesTheTrigger()
    {
        // Arrange
        var useCase = CreateUseCase();

        // Act
        await useCase.SaveAsync(Interval, CancellationToken.None);

        // Assert
        _trigger.Received(1).RequestRefresh();
    }

    [Fact]
    public async Task SaveAsync_WhenTheStoreThrows_PropagatesAndDoesNotPokeTheTrigger()
    {
        // Arrange
        _store
            .SetPollIntervalAsync(Interval, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("write failed"));

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateUseCase().SaveAsync(Interval, CancellationToken.None)
        );
        _trigger.DidNotReceive().RequestRefresh();
    }

    private SavePollInterval CreateUseCase() => new(_store, _trigger);
}
