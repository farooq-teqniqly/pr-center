using Microsoft.Data.Sqlite;
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
        _options = new DbContextOptionsBuilder<PrCenterDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        using var context = CreateContext();
        context.Database.Migrate();
    }

    /// <summary>Creates a fresh context over the temporary database.</summary>
    /// <returns>A new <see cref="PrCenterDbContext"/>.</returns>
    public PrCenterDbContext CreateContext() => new(_options);

    /// <inheritdoc />
    public void Dispose()
    {
        // Release pooled connections so the file handle is freed before delete.
        SqliteConnection.ClearAllPools();

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
