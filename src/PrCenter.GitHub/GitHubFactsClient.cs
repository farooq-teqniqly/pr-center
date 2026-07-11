using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PrCenter.Core.Facts;
using PrCenter.Core.Ports;

namespace PrCenter.GitHub;

/// <summary>
/// Adapter implementing <see cref="IGitHubFacts"/> against the GitHub GraphQL
/// API. Discovery unions the review-requested and reviewed-by searches; the
/// per-request bearer token comes from the token vault.
/// </summary>
internal sealed class GitHubFactsClient : IGitHubFacts
{
    private readonly HttpClient _httpClient;
    private readonly ITokenVault _tokenVault;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubFactsClient"/> class.
    /// </summary>
    /// <param name="httpClient">The configured GitHub HTTP client.</param>
    /// <param name="tokenVault">The vault supplying each owner's access token.</param>
    public GitHubFactsClient(HttpClient httpClient, ITokenVault tokenVault)
    {
        _httpClient = httpClient;
        _tokenVault = tokenVault;
    }

    /// <inheritdoc />
    public Task<string> GetAuthenticatedUserLoginAsync(
        string owner,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    /// <inheritdoc />
    public async Task<OwnerFactsResult> GetReviewQueueFactsAsync(
        string owner,
        string myLogin,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        var token = await _tokenVault.GetTokenAsync(owner, cancellationToken).ConfigureAwait(false);

        var variables = new
        {
            requested = $"is:pr is:open review-requested:{myLogin}",
            reviewed = $"is:pr is:open reviewed-by:{myLogin}",
        };
        var payload = new { query = GitHubGraphQlQueries.ReviewQueue, variables };

        using var request = new HttpRequestMessage(HttpMethod.Post, "graphql")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response
            .Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var data = document.RootElement.GetProperty("data");
        return new OwnerFactsResult(OwnerFetchStatus.Ok, UnionFacts(data));
    }

    /// <inheritdoc />
    public Task<PullRequestFacts?> GetPullRequestFactsAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    private static IReadOnlyList<PullRequestFacts> UnionFacts(JsonElement data)
    {
        var byId = new Dictionary<string, PullRequestFacts>(StringComparer.Ordinal);

        foreach (var search in (ReadOnlySpan<string>)["requested", "reviewed"])
        {
            // Both aliases are guaranteed by the query; a missing one is a
            // malformed response and throws, surfacing as an Error status.
            foreach (var node in data.GetProperty(search).GetProperty("nodes").EnumerateArray())
            {
                var facts = PullRequestFactsMapper.MapPullRequest(node);

                // Both searches return identical data for a shared PR, so the
                // first search to yield an id wins and the duplicate is ignored.
                byId.TryAdd(facts.Identity.Id, facts);
            }
        }

        return byId.Values.ToArray();
    }
}
