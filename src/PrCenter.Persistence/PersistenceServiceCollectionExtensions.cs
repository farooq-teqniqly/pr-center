using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence;

/// <summary>
/// Registration entry point for the persistence adapter, letting the
/// composition root bind <see cref="IStateStore"/> and <see cref="ITokenVault"/>
/// without seeing the internal adapter types.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// The fail-fast ceiling for a SQLite command: a write that cannot acquire
    /// its lock within this window errors rather than hanging the caller. SQLite
    /// has no server-side execution killer, so this bounds the lock-acquisition
    /// wait.
    /// </summary>
    private const int CommandTimeoutSeconds = 5;

    /// <summary>
    /// Registers the SQLite context and the persistence adapter's
    /// implementations of <see cref="IStateStore"/> and <see cref="ITokenVault"/>.
    /// </summary>
    /// <param name="services">The service collection to add the adapter to.</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="isDevelopment">
    /// When <see langword="true"/>, enables <c>EnableSensitiveDataLogging</c> and
    /// detailed errors so parameter values are visible while debugging locally.
    /// Must be <see langword="false"/> outside Development so parameter values are
    /// never written to a container or production log.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is null or whitespace.</exception>
    public static IServiceCollection AddPersistenceAdapter(
        this IServiceCollection services,
        string connectionString,
        bool isDevelopment
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<PrCenterDbContext>(options =>
        {
            options
                .UseSqlite(connectionString, sqlite => sqlite.CommandTimeout(CommandTimeoutSeconds))
                .AddInterceptors(SqlitePragmaInterceptor.Instance);

            if (isDevelopment)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });
        services.AddScoped<IStateStore, StateStore>();
        services.AddScoped<ITokenVault, TokenVault>();
        return services;
    }
}
