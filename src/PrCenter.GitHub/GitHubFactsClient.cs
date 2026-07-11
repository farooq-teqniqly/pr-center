using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PrCenter.Core.Facts;
using PrCenter.Core.Ports;

namespace PrCenter.GitHub;

/// <summary>
/// Adapter implementing <see cref="IGitHubFacts"/> against the GitHub GraphQL
/// API. Discovery unions the review-requested and reviewed-by searches; the
/// per-request bearer token comes from the token vault. A fetch failure is
/// reported as a <see cref="OwnerFetchStatus"/>, never thrown, so one owner
/// cannot abort a poll covering others.
/// </summary>
internal sealed partial class GitHubFactsClient : IGitHubFacts
{
    private const string UserAgentProduct = "PrCenter";
    private const string UserAgentVersion = "1.0";

    private readonly HttpClient _httpClient;
    private readonly ITokenVault _tokenVault;
    private readonly ILogger<GitHubFactsClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubFactsClient"/> class.
    /// </summary>
    /// <param name="httpClient">The configured GitHub HTTP client.</param>
    /// <param name="tokenVault">The vault supplying each owner's access token.</param>
    /// <param name="logger">The logger for fetch failures.</param>
    public GitHubFactsClient(
        HttpClient httpClient,
        ITokenVault tokenVault,
        ILogger<GitHubFactsClient> logger
    )
    {
        _httpClient = httpClient;
        _tokenVault = tokenVault;
        _logger = logger;
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

        try
        {
            var token = await _tokenVault
                .GetTokenAsync(owner, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                LogTokenMissing(owner);
                return Failure(
                    OwnerFetchStatus.MisconfiguredToken,
                    "No token is configured for this owner."
                );
            }

            using var request = BuildReviewQueueRequest(myLogin, token);
            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return await ClassifyAsync(owner, response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            LogFetchFailed(owner, exception.Message);
            return Failure(OwnerFetchStatus.Error, "A network error occurred contacting GitHub.");
        }
        catch (JsonException exception)
        {
            LogFetchFailed(owner, exception.Message);
            return Failure(OwnerFetchStatus.Error, "GitHub returned a malformed response.");
        }
    }

    /// <inheritdoc />
    public Task<PullRequestFacts?> GetPullRequestFactsAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default
    ) => throw new NotImplementedException();

    private async Task<OwnerFactsResult> ClassifyAsync(
        string owner,
        HttpResponseMessage response,
        CancellationToken cancellationToken
    )
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            LogFetchFailed(owner, "unauthorized");
            return Failure(
                OwnerFetchStatus.MisconfiguredToken,
                "The token was rejected (401 Unauthorized)."
            );
        }

        if (!response.IsSuccessStatusCode)
        {
            var reason = $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}.";
            LogFetchFailed(owner, reason);
            return Failure(OwnerFetchStatus.Error, reason);
        }

        await using var stream = await response
            .Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return MapDocument(owner, document.RootElement);
    }

    private OwnerFactsResult MapDocument(string owner, JsonElement root)
    {
        if (
            root.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0
        )
        {
            var (status, detail) = ClassifyGraphQlErrors(errors);
            LogFetchFailed(owner, detail);
            return Failure(status, detail);
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            LogFetchFailed(owner, "no data");
            return Failure(OwnerFetchStatus.Error, "GitHub returned a response with no data.");
        }

        return new OwnerFactsResult(OwnerFetchStatus.Ok, UnionFacts(data));
    }

    private static (OwnerFetchStatus Status, string Detail) ClassifyGraphQlErrors(
        JsonElement errors
    )
    {
        var types = errors
            .EnumerateArray()
            .Select(error =>
                error.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
                    ? type.GetString()
                    : null
            )
            .ToList();

        if (types.Contains("FORBIDDEN") || types.Contains("INSUFFICIENT_SCOPES"))
        {
            return (
                OwnerFetchStatus.MisconfiguredToken,
                "The token lacks the permissions this owner requires."
            );
        }

        if (types.Contains("RATE_LIMITED"))
        {
            return (OwnerFetchStatus.Error, "The GitHub API rate limit is exhausted.");
        }

        return (OwnerFetchStatus.Error, "GitHub returned a GraphQL error.");
    }

    private static HttpRequestMessage BuildReviewQueueRequest(string myLogin, string token)
    {
        var variables = new
        {
            requested = $"is:pr is:open review-requested:{myLogin}",
            reviewed = $"is:pr is:open reviewed-by:{myLogin}",
        };
        var payload = new { query = GitHubGraphQlQueries.ReviewQueue, variables };

        var request = new HttpRequestMessage(HttpMethod.Post, "graphql")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.Add(
            new ProductInfoHeaderValue(UserAgentProduct, UserAgentVersion)
        );
        return request;
    }

    private static OwnerFactsResult Failure(OwnerFetchStatus status, string detail) =>
        new(status, [], detail);

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

        return [.. byId.Values];
    }
}
