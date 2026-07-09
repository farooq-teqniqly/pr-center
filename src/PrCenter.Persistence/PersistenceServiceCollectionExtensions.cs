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
    /// Registers the SQLite context and the persistence adapter's
    /// implementations of <see cref="IStateStore"/> and <see cref="ITokenVault"/>.
    /// </summary>
    /// <param name="services">The service collection to add the adapter to.</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is null or whitespace.</exception>
    public static IServiceCollection AddPersistenceAdapter(
        this IServiceCollection services,
        string connectionString
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<PrCenterDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IStateStore, StateStore>();
        services.AddScoped<ITokenVault, TokenVault>();
        return services;
    }
}
