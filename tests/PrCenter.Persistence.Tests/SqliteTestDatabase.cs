using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence.Tests;

/// <summary>
/// A real SQLite database backed by a unique temporary file, migrated on
/// construction and deleted on dispose. Integration tests use this instead of
/// an in-memory database so they exercise the real provider, schema, and
/// migrations. Reusable by later persistence changes (token vault, settings).
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _path;
    private readonly DbContextOptions<PrCenterDbContext> _options;

    /// <summary>
    /// Creates a unique temporary database file and applies all migrations to it.
    /// </summary>
    public SqliteTestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"prcenter-test-{Guid.NewGuid():N}.db");

        // Same SQLite configuration as production (WAL + busy timeout + command
        // timeout) so the concurrent-writer test is representative. Pooling=False
        // so disposing a context fully closes its connection and frees the file
        // handle for delete, without the process-wide
        // SqliteConnection.ClearAllPools() -- which would close pooled connections
        // that a parallel test class is still using.
        var builder = new DbContextOptionsBuilder<PrCenterDbContext>();
        SqliteContextConfiguration.Configure(builder, $"Data Source={_path};Pooling=False");
        _options = builder.Options;

        using var context = CreateContext();
        context.Database.Migrate();
    }

    /// <summary>Creates a fresh context over the temporary database.</summary>
    /// <returns>A new <see cref="PrCenterDbContext"/>.</returns>
    public PrCenterDbContext CreateContext() => new(_options);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var suffix in (ReadOnlySpan<string>)["", "-wal", "-shm"])
        {
            var file = _path + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
