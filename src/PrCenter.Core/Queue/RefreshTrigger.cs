using System.Threading.Channels;

namespace PrCenter.Core.Queue;

/// <summary>
/// Channel-backed refresh trigger shared by the poll loop and its pokers. A
/// bounded capacity-1, drop-write channel is the coalescing signal: a poke writes
/// one token, further pokes while that token is unconsumed are dropped, so many
/// requests collapse into at most one pending wake. The single poll loop is the
/// only consumer, so polls never overlap.
/// </summary>
public sealed class RefreshTrigger : IRefreshTrigger
{
    private readonly Channel<byte> _signal = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
        }
    );

    /// <inheritdoc />
    public void RequestRefresh() => _signal.Writer.TryWrite(0);

    /// <summary>
    /// Waits for the next refresh request, completing immediately when a poke is
    /// already pending. Intended for the single poll loop.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    /// <returns>A task that completes when a refresh has been requested.</returns>
    /// <exception cref="OperationCanceledException">The wait was canceled.</exception>
    public async ValueTask WaitForRequestAsync(CancellationToken cancellationToken = default) =>
        await _signal.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
}
