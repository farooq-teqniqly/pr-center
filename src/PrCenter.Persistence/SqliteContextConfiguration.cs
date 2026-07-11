using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence;

/// <summary>
/// Single source of truth for the SQLite context configuration shared by the
/// registered adapter and the integration-test harness: the connection, the
/// fail-fast command timeout, and the pragma interceptor (WAL + busy timeout).
/// Keeping tests on the same configuration keeps the concurrent-writer coverage
/// representative of production.
/// </summary>
internal static class SqliteContextConfiguration
{
    /// <summary>
    /// The fail-fast ceiling for a SQLite command: a write that cannot acquire
    /// its lock within this window errors rather than hanging the caller. SQLite
    /// has no server-side execution killer, so this bounds the lock-acquisition
    /// wait.
    /// </summary>
    internal const int CommandTimeoutSeconds = 5;

    /// <summary>
    /// Applies the SQLite provider, the command timeout, and the pragma
    /// interceptor to the given options builder.
    /// </summary>
    /// <param name="options">The options builder to configure.</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    internal static void Configure(DbContextOptionsBuilder options, string connectionString)
    {
        options
            .UseSqlite(connectionString, sqlite => sqlite.CommandTimeout(CommandTimeoutSeconds))
            .AddInterceptors(SqlitePragmaInterceptor.Instance);
    }
}
