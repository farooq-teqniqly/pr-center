namespace PrCenter.Core.Ports;

/// <summary>
/// Port for reading raw pull-request facts from GitHub for a single owner.
/// The adapter is transport-neutral (REST or GraphQL); feature changes extend
/// this surface as derivation needs grow.
/// </summary>
public interface IGitHubFacts
{
    /// <summary>
    /// Gets the login of the GitHub user the owner's personal access token
    /// authenticates as, used to scope all "relative to the user" evaluation.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) whose token to use.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The authenticated user's login.</returns>
    Task<string> GetAuthenticatedUserLoginAsync(
        string owner,
        CancellationToken cancellationToken = default
    );
}
