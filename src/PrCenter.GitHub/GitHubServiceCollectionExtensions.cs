using Microsoft.Extensions.DependencyInjection;
using PrCenter.Core.Ports;

namespace PrCenter.GitHub;

/// <summary>
/// Registration entry point for the GitHub adapter, letting the composition
/// root bind <see cref="IGitHubFacts"/> without seeing the internal adapter type.
/// </summary>
public static class GitHubServiceCollectionExtensions
{
    /// <summary>
    /// Registers the GitHub adapter's implementation of <see cref="IGitHubFacts"/>.
    /// </summary>
    /// <param name="services">The service collection to add the adapter to.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddGitHubAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IGitHubFacts, GitHubFactsClient>();
        return services;
    }
}
