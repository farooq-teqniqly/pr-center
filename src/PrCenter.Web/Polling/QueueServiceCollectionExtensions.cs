using Microsoft.Extensions.DependencyInjection;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;

namespace PrCenter.Web.Polling;

/// <summary>
/// Registration entry point for the polling and refresh services: the process
/// singletons (snapshot holder, refresh trigger, clock), the scoped use cases,
/// and the background poll loop.
/// </summary>
internal static class QueueServiceCollectionExtensions
{
    /// <summary>
    /// Registers the queue snapshot holder, refresh trigger, clock, the
    /// refresh/read/unlock and settings use cases, and the polling background
    /// service.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    public static IServiceCollection AddQueueServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Process-wide state and signals shared across circuits.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<QueueSnapshotHolder>();
        services.AddSingleton<RefreshTrigger>();
        services.AddSingleton<IRefreshTrigger>(sp => sp.GetRequiredService<RefreshTrigger>());

        // Use cases run inside a request/poll scope so they see the scoped ports.
        services.AddScoped<RefreshQueue>();
        services.AddScoped<IRefreshQueue>(sp => sp.GetRequiredService<RefreshQueue>());
        services.AddScoped<GetQueue>();
        services.AddScoped<UnlockApp>();
        services.AddScoped<InitializeVault>();
        services.AddScoped<SaveOwnerToken>();
        services.AddScoped<RemoveOwner>();
        services.AddScoped<SavePollInterval>();

        services.AddHostedService<QueuePollingService>();
        return services;
    }
}
