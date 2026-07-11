using System.Net;
using System.Text;
using NSubstitute;
using PrCenter.Core.Ports;

namespace PrCenter.GitHub.Tests;

/// <summary>
/// Builds a <see cref="GitHubFactsClient"/> backed by a faked handler and token
/// vault, tracking the created disposables and the last outgoing request.
/// </summary>
internal sealed class GitHubClientHarness : IDisposable
{
    private readonly List<IDisposable> _disposables = [];

    /// <summary>Gets the most recent request the client sent, if any.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>Builds a client whose single HTTP call returns <paramref name="response"/>.</summary>
    /// <param name="response">The response the faked handler returns.</param>
    /// <param name="token">The token the vault returns for the fixture owner.</param>
    /// <returns>The configured client.</returns>
    public GitHubFactsClient Build(HttpResponseMessage response, string? token = "token")
    {
        _disposables.Add(response);

        var handler = Substitute.For<FakeHttpMessageHandler>();
        handler
            .MockSendAsync(
                Arg.Do<HttpRequestMessage>(request => LastRequest = request),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(response));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        _disposables.Add(httpClient);

        var vault = Substitute.For<ITokenVault>();
        vault.GetTokenAsync(GraphQlFixtures.Owner, Arg.Any<CancellationToken>()).Returns(token);

        return new GitHubFactsClient(httpClient, vault, new CapturingLogger<GitHubFactsClient>());
    }

    /// <summary>Creates a 200 response with the given JSON body.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>The response.</returns>
    public static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }
}
