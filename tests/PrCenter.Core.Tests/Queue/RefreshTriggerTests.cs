namespace PrCenter.Core.Tests.Queue;

using PrCenter.Core.Queue;

public sealed class RefreshTriggerTests
{
    [Fact]
    public async Task RequestRefresh_WhenConsumerIsWaiting_CompletesTheWait()
    {
        // Arrange
        var trigger = new RefreshTrigger();
        var wait = trigger.WaitForRequestAsync(CancellationToken.None).AsTask();
        Assert.False(wait.IsCompleted);

        // Act
        trigger.RequestRefresh();

        // Assert
        await wait;
    }

    [Fact]
    public async Task RequestRefresh_CalledMultipleTimes_CoalescesToOneSignal()
    {
        // Arrange
        var trigger = new RefreshTrigger();
        trigger.RequestRefresh();
        trigger.RequestRefresh();
        trigger.RequestRefresh();

        // Act
        await trigger.WaitForRequestAsync(CancellationToken.None);

        // Assert
        var second = trigger.WaitForRequestAsync(CancellationToken.None).AsTask();
        Assert.False(second.IsCompleted);
    }

    [Fact]
    public async Task RequestRefresh_WithNoConsumerWaiting_IsNotLost()
    {
        // Arrange
        var trigger = new RefreshTrigger();

        // Act
        trigger.RequestRefresh();

        // Assert
        var wait = trigger.WaitForRequestAsync(CancellationToken.None);
        Assert.True(wait.IsCompleted);
        await wait;
    }
}
