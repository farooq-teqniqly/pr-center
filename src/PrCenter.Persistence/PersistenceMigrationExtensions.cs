using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PrCenter.Persistence;

/// <summary>
/// Startup hook that applies pending EF Core migrations for the persistence
/// adapter, keeping the internal <see cref="PrCenterDbContext"/> hidden from the
/// composition root.
/// </summary>
public static class PersistenceMigrationExtensions
{
    /// <summary>
    /// Applies any pending migrations to the SQLite database, creating or
    /// evolving the schema. Runs before the app is unlocked -- the schema is not
    /// secret, so migration needs no decrypted key.
    /// </summary>
    /// <param name="services">The host's root service provider.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when migrations have been applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static async Task MigratePersistenceAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PrCenterDbContext>();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
