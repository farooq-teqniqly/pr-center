namespace PrCenter.Core.Ports;

/// <summary>
/// Port for persisting the user's per-pull-request "last looked at" state, the
/// basis for the read-vs-unread model. Feature changes extend this surface.
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Gets the instant the user last looked at the given pull request, or
    /// <see langword="null"/> if they never have.
    /// </summary>
    /// <param name="pullRequestId">The stable identifier of the pull request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The last-seen instant, or <see langword="null"/> if unseen.</returns>
    Task<DateTimeOffset?> GetLastSeenAsync(
        string pullRequestId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Records that the user has looked at the given pull request at the given
    /// instant.
    /// </summary>
    /// <param name="pullRequestId">The stable identifier of the pull request.</param>
    /// <param name="seenAt">The instant the user looked at the pull request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the state is persisted.</returns>
    Task SetLastSeenAsync(
        string pullRequestId,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken = default
    );
}
