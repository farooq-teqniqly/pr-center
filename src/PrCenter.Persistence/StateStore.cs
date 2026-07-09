using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IStateStore"/> over the SQLite context.
/// A skeleton stub for now: members throw <see cref="NotImplementedException"/>
/// until the state-persistence change specifies their behavior.
/// </summary>
internal sealed class StateStore : IStateStore
{
    /// <inheritdoc />
    public Task<DateTimeOffset?> GetLastSeenAsync(
        string pullRequestId,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task SetLastSeenAsync(
        string pullRequestId,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();
}
