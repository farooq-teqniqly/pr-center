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
    /// Registers the GitHub adapter's typed <see cref="IGitHubFacts"/> client and
    /// returns its builder so the composition root can configure the base address
    /// and a resilience handler.
    /// </summary>
    /// <param name="services">The service collection to add the adapter to.</param>
    /// <returns>The HTTP client builder for the typed client.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IHttpClientBuilder AddGitHubAdapter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Typed client: the HttpClient is injected into GitHubFactsClient. The
        // base address and resilience handler are configured by the caller so the
        // resilience dependency stays in the composition root.
        return services.AddHttpClient<IGitHubFacts, GitHubFactsClient>();
    }
}
