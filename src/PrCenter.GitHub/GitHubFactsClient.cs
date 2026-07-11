using PrCenter.Core.Facts;
using PrCenter.Core.Ports;

namespace PrCenter.GitHub;

/// <summary>
/// Adapter implementing <see cref="IGitHubFacts"/> against the GitHub API.
/// A skeleton stub for now: members throw <see cref="NotImplementedException"/>
/// until the GitHub adapter change specifies their behavior.
/// </summary>
internal sealed class GitHubFactsClient : IGitHubFacts
{
    /// <inheritdoc />
    public Task<string> GetAuthenticatedUserLoginAsync(
        string owner,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<OwnerFactsResult> GetReviewQueueFactsAsync(
        string owner,
        string myLogin,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<PullRequestFacts?> GetPullRequestFactsAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();
}
