using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PrCenter.Persistence;

/// <summary>
/// Applies the per-connection SQLite pragmas the state store relies on for safe
/// concurrent access: WAL journal mode (readers never block the single writer)
/// and a busy timeout (a writer waits out lock contention instead of failing
/// immediately). WAL is persisted in the database header, but the busy timeout
/// is per-connection, so both are (re)applied every time a connection opens.
/// </summary>
internal sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    /// <summary>
    /// How long SQLite's internal busy handler retries a locked write before
    /// giving up. Kept below the 5-second command timeout so the command timeout
    /// stays the outer fail-fast ceiling.
    /// </summary>
    private const int BusyTimeoutMilliseconds = 3000;

    // busy_timeout is armed first so the journal_mode=WAL change -- itself a write
    // that can hit SQLITE_BUSY under contention -- is covered by the busy handler
    // rather than failing immediately.
    private static readonly string PragmaSql =
        $"PRAGMA busy_timeout={BusyTimeoutMilliseconds}; PRAGMA journal_mode=WAL;";

    /// <summary>A shared, stateless instance for use in context configuration.</summary>
    public static SqlitePragmaInterceptor Instance { get; } = new();

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = connection.CreateCommand();
        command.CommandText = PragmaSql;
        command.ExecuteNonQuery();

        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(connection);

        await using var command = connection.CreateCommand();
        command.CommandText = PragmaSql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken)
            .ConfigureAwait(false);
    }
}
