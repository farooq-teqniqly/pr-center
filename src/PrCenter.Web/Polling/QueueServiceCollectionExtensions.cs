using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PrCenter.Core.Queue;

namespace PrCenter.Web.Polling;

/// <summary>
/// Registration entry point for the polling and refresh services: the process
/// singletons (snapshot holder, refresh trigger, clock), the scoped use cases,
/// the bound poll options, and the background poll loop.
/// </summary>
internal static class QueueServiceCollectionExtensions
{
    /// <summary>
    /// Registers the queue snapshot holder, refresh trigger, clock, refresh/read/
    /// unlock use cases, poll options bound from configuration, and the polling
    /// background service.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configuration">The configuration the poll options bind from.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is null.
    /// </exception>
    public static IServiceCollection AddQueueServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

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

        services.AddSingleton<IValidateOptions<PollingOptions>, PollingOptionsValidator>();
        services
            .AddOptions<PollingOptions>()
            .Bind(configuration.GetSection(PollingOptions.SectionName))
            .ValidateOnStart();
        services.AddHostedService<QueuePollingService>();
        return services;
    }
}
