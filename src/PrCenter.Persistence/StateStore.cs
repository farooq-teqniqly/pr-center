using Microsoft.EntityFrameworkCore;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IStateStore"/> over the SQLite context.
/// Markers are keyed by pull request id, upserted on set, and never deleted.
/// </summary>
internal sealed class StateStore : IStateStore
{
    private readonly PrCenterDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateStore"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    public StateStore(PrCenterDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="pullRequestId"/> is null, empty, or whitespace.
    /// </exception>
    public async Task<DateTimeOffset?> GetLastSeenAsync(
        string pullRequestId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pullRequestId);

        // Read-only: no tracking, and project just the instant -- no entity is
        // materialized or tracked.
        return await _context
            .LastSeenMarkers.AsNoTracking()
            .Where(marker => marker.PullRequestId == pullRequestId)
            .Select(marker => (DateTimeOffset?)marker.SeenAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="pullRequestId"/> is null, empty, or whitespace.
    /// </exception>
    public async Task SetLastSeenAsync(
        string pullRequestId,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pullRequestId);

        var marker = await _context
            .LastSeenMarkers.FindAsync([pullRequestId], cancellationToken)
            .ConfigureAwait(false);
        if (marker is null)
        {
            _context.LastSeenMarkers.Add(
                new LastSeenMarker { PullRequestId = pullRequestId, SeenAt = seenAt }
            );
        }
        else
        {
            marker.SeenAt = seenAt;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
