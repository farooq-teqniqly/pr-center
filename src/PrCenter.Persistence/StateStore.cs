using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Adapter implementing <see cref="IStateStore"/> over the SQLite context.
/// Markers are keyed by pull request id, upserted on set, and never deleted.
/// </summary>
internal sealed partial class StateStore : IStateStore
{
    private readonly PrCenterDbContext _context;
    private readonly ILogger<StateStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateStore"/> class.
    /// </summary>
    /// <param name="context">The SQLite context.</param>
    /// <param name="logger">The logger for recovery diagnostics.</param>
    public StateStore(PrCenterDbContext context, ILogger<StateStore> logger)
    {
        _context = context;
        _logger = logger;
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
        if (marker is not null)
        {
            marker.SeenAt = seenAt;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var inserted = new LastSeenMarker { PullRequestId = pullRequestId, SeenAt = seenAt };
        _context.LastSeenMarkers.Add(inserted);
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            var recovered = await TryRecoverFromInsertRaceAsync(inserted, seenAt, cancellationToken)
                .ConfigureAwait(false);
            if (!recovered)
            {
                // Not a lost insert race -- a genuine write failure. Bare re-throw
                // preserves the original stack trace.
                throw;
            }
        }
    }

    /// <summary>
    /// Attempts to recover from losing the insert race: a concurrent writer
    /// committed a marker for the same id between this call's lookup and its save,
    /// so the insert failed on the primary key. Detaches the failed insert and
    /// updates the winner's row instead, preserving the upsert contract.
    /// </summary>
    /// <param name="inserted">The marker whose insert lost the race.</param>
    /// <param name="seenAt">The instant to store on the winning row.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when a winning row was found and updated;
    /// <see langword="false"/> when no such row exists, meaning the failure was
    /// not an insert race and the caller should re-throw.
    /// </returns>
    private async Task<bool> TryRecoverFromInsertRaceAsync(
        LastSeenMarker inserted,
        DateTimeOffset seenAt,
        CancellationToken cancellationToken
    )
    {
        _context.Entry(inserted).State = EntityState.Detached;

        var winner = await _context
            .LastSeenMarkers.FindAsync([inserted.PullRequestId], cancellationToken)
            .ConfigureAwait(false);
        if (winner is null)
        {
            return false;
        }

        LogRecoveredFromInsertRace(inserted.PullRequestId);
        winner.SeenAt = seenAt;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
