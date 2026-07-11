using System.Globalization;
using System.Net;
using System.Text;
using NSubstitute;
using PrCenter.Core.Facts;
using PrCenter.Core.Ports;

namespace PrCenter.GitHub.Tests;

public sealed class GetReviewQueueFactsAsyncTests : IDisposable
{
    private readonly List<HttpResponseMessage> _responses = [];
    private readonly List<HttpClient> _httpClients = [];

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenFetchSucceeds_ReturnsOk()
    {
        // Act
        var result = await FetchQueueAsync(GraphQlFixtures.ReviewQueueResponse);

        // Assert
        Assert.Equal(OwnerFetchStatus.Ok, result.Status);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_UnionsAndDeduplicatesById()
    {
        // Act
        var result = await FetchQueueAsync(GraphQlFixtures.ReviewQueueResponse);

        // Assert
        Assert.Equal(["A", "B", "C"], result.Facts.Select(f => f.Identity.Id).Order());
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_IncludesReviewedOnlyPullRequest()
    {
        // Act
        var result = await FetchQueueAsync(GraphQlFixtures.ReviewQueueResponse);

        // Assert
        Assert.Contains(result.Facts, f => f.Identity.Id == "C");
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_MapsBotFlagFromTypenameAndOmitsDismissed()
    {
        // Act
        var reviews = (await FactsForAsync("A")).Activity.Reviews;

        // Assert
        Assert.Equal(2, reviews.Count);
        Assert.False(Single(reviews, "human-rev").IsBot);
        Assert.True(Single(reviews, "qodo").IsBot);
        Assert.DoesNotContain(reviews, r => r.ReviewerLogin == "dismissed-rev");
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_MapsCommitLandDateAndAuthorEmailFallback()
    {
        // Act
        var commit = Assert.Single((await FactsForAsync("A")).Activity.Commits);

        // Assert
        Assert.Equal("unlinked@example.com", commit.AuthorLogin);
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-01T07:00:00Z", CultureInfo.InvariantCulture),
            commit.LandedAt
        );
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_MapsCommentsFromIssueAndInlineSources()
    {
        // Act
        var comments = (await FactsForAsync("A")).Activity.Comments;

        // Assert
        Assert.Equal(2, comments.Count);
        Assert.False(Single(comments, "human-commenter").IsBot);
        Assert.True(Single(comments, "Copilot").IsBot);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_SkipsNonUserReviewRequests()
    {
        // Act
        var requested = (await FactsForAsync("A")).Activity.RequestedReviewerLogins;

        // Assert
        Assert.Equal(["octocat"], requested);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_MapsDraftAndOpenStatus()
    {
        // Act
        var draft = await FactsForAsync("B");
        var open = await FactsForAsync("A");

        // Assert
        Assert.True(draft.Status.IsDraft);
        Assert.False(open.Status.IsDraft);
        Assert.False(open.Status.IsClosedOrMerged);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_MapsApprovedReview()
    {
        // Act
        var result = await FetchQueueAsync(GraphQlFixtures.MappingEdgeCasesResponse);

        // Assert
        var review = Assert.Single(result.Facts.Single().Activity.Reviews);
        Assert.Equal(ReviewState.Approved, review.State);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_ResolvesCommitAuthorByLoginThenEmailNameOrUnknown()
    {
        // Act
        var result = await FetchQueueAsync(GraphQlFixtures.MappingEdgeCasesResponse);

        // Assert
        var commitAuthors = result
            .Facts.Single()
            .Activity.Commits.Select(c => c.AuthorLogin)
            .Order(StringComparer.Ordinal);
        Assert.Equal(["Only Name", "linked-dev", "unknown"], commitAuthors);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_SkipsCommentsWithNoAuthorAndMissingThreads()
    {
        // Act
        var result = await FetchQueueAsync(GraphQlFixtures.MappingEdgeCasesResponse);

        // Assert
        Assert.Empty(result.Facts.Single().Activity.Comments);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_SendsVaultTokenAsBearerWithUserAgent()
    {
        // Act
        var run = await RunAsync(Ok(GraphQlFixtures.EmptyResultsResponse), token: "secret-token");

        // Assert
        Assert.Equal("Bearer", run.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", run.Request.Headers.Authorization.Parameter);
        Assert.NotEmpty(run.Request.Headers.UserAgent);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenEmpty_ReturnsOkWithNoFacts()
    {
        // Act
        var run = await RunAsync(Ok(GraphQlFixtures.EmptyResultsResponse));

        // Assert
        Assert.Equal(OwnerFetchStatus.Ok, run.Result.Status);
        Assert.Empty(run.Result.Facts);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenTokenMissing_ReturnsMisconfiguredToken()
    {
        // Act
        var run = await RunAsync(Ok(GraphQlFixtures.EmptyResultsResponse), token: null);

        // Assert
        Assert.Equal(OwnerFetchStatus.MisconfiguredToken, run.Result.Status);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenUnauthorized_ReturnsMisconfiguredToken()
    {
        // Act
        var run = await RunAsync(Status(HttpStatusCode.Unauthorized, "{}"));

        // Assert
        Assert.Equal(OwnerFetchStatus.MisconfiguredToken, run.Result.Status);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenGraphQlForbidden_ReturnsMisconfiguredToken()
    {
        // Act
        var run = await RunAsync(Ok(GraphQlFixtures.ForbiddenErrorsResponse));

        // Assert
        Assert.Equal(OwnerFetchStatus.MisconfiguredToken, run.Result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    public async Task GetReviewQueueFactsAsync_WhenServerFails_ReturnsError(
        HttpStatusCode statusCode,
        string body
    )
    {
        // Act
        var run = await RunAsync(Status(statusCode, body));

        // Assert
        Assert.Equal(OwnerFetchStatus.Error, run.Result.Status);
        Assert.NotNull(run.Result.Detail);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenRateLimited_ReturnsError()
    {
        // Act
        var run = await RunAsync(Ok(GraphQlFixtures.RateLimitedErrorsResponse));

        // Assert
        Assert.Equal(OwnerFetchStatus.Error, run.Result.Status);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenPayloadMalformed_ReturnsError()
    {
        // Act
        var run = await RunAsync(Ok("this is not json"));

        // Assert
        Assert.Equal(OwnerFetchStatus.Error, run.Result.Status);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_NeverLeaksTokenInDetailOrLogs()
    {
        // Arrange / Act
        var run = await RunAsync(
            Status(HttpStatusCode.Unauthorized, "{}"),
            token: "super-secret-pat"
        );

        // Assert
        Assert.NotEmpty(run.Logs);
        Assert.DoesNotContain("super-secret-pat", run.Result.Detail ?? string.Empty);
        Assert.DoesNotContain(
            run.Logs,
            message => message.Contains("super-secret-pat", StringComparison.Ordinal)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetReviewQueueFactsAsync_WithMissingOwner_Throws(string? owner)
    {
        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            GuardTestClient().GetReviewQueueFactsAsync(owner!, "octocat", CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetReviewQueueFactsAsync_WithMissingMyLogin_Throws(string? myLogin)
    {
        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            GuardTestClient()
                .GetReviewQueueFactsAsync(GraphQlFixtures.Owner, myLogin!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenNetworkFails_ReturnsError()
    {
        // Act
        var run = await RunThrowingAsync(new HttpRequestException("boom"));

        // Assert
        Assert.Equal(OwnerFetchStatus.Error, run.Result.Status);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenResponseHasNoData_ReturnsError()
    {
        // Act
        var run = await RunAsync(Ok("{}"));

        // Assert
        Assert.Equal(OwnerFetchStatus.Error, run.Result.Status);
    }

    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenGraphQlErrorIsUnrecognized_ReturnsError()
    {
        // Act
        var run = await RunAsync(
            Ok("""{ "errors": [ { "type": "SOMETHING_ELSE", "message": "x" } ] }""")
        );

        // Assert
        Assert.Equal(OwnerFetchStatus.Error, run.Result.Status);
    }

    private GitHubFactsClient GuardTestClient()
    {
        var httpClient = new HttpClient();
        _httpClients.Add(httpClient);
        return new GitHubFactsClient(
            httpClient,
            Substitute.For<ITokenVault>(),
            new CapturingLogger<GitHubFactsClient>()
        );
    }

    private async Task<Run> RunThrowingAsync(Exception exception)
    {
        var handler = Substitute.For<FakeHttpMessageHandler>();
        handler
            .MockSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<HttpResponseMessage>(exception));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        _httpClients.Add(httpClient);

        var vault = Substitute.For<ITokenVault>();
        vault.GetTokenAsync(GraphQlFixtures.Owner, Arg.Any<CancellationToken>()).Returns("token");

        var logger = new CapturingLogger<GitHubFactsClient>();
        var client = new GitHubFactsClient(httpClient, vault, logger);
        var result = await client.GetReviewQueueFactsAsync(
            GraphQlFixtures.Owner,
            "octocat",
            CancellationToken.None
        );

        return new Run(result, null, logger.Messages);
    }

    private async Task<PullRequestFacts> FactsForAsync(string id)
    {
        var result = await FetchQueueAsync(GraphQlFixtures.ReviewQueueResponse);
        return result.Facts.Single(f => f.Identity.Id == id);
    }

    private async Task<OwnerFactsResult> FetchQueueAsync(string responseBody) =>
        (await RunAsync(Ok(responseBody))).Result;

    private HttpResponseMessage Ok(string body) => Status(HttpStatusCode.OK, body);

    private HttpResponseMessage Status(HttpStatusCode statusCode, string body)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        _responses.Add(response);
        return response;
    }

    private async Task<Run> RunAsync(HttpResponseMessage response, string? token = "token")
    {
        HttpRequestMessage? captured = null;
        var handler = Substitute.For<FakeHttpMessageHandler>();
        handler
            .MockSendAsync(
                Arg.Do<HttpRequestMessage>(request => captured = request),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(response));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        _httpClients.Add(httpClient);

        var vault = Substitute.For<ITokenVault>();
        vault.GetTokenAsync(GraphQlFixtures.Owner, Arg.Any<CancellationToken>()).Returns(token);

        var logger = new CapturingLogger<GitHubFactsClient>();
        var client = new GitHubFactsClient(httpClient, vault, logger);
        var result = await client.GetReviewQueueFactsAsync(
            GraphQlFixtures.Owner,
            "octocat",
            CancellationToken.None
        );

        return new Run(result, captured, logger.Messages);
    }

    private sealed record Run(
        OwnerFactsResult Result,
        HttpRequestMessage? Request,
        IReadOnlyList<string> Logs
    );

    private static ReviewFact Single(IEnumerable<ReviewFact> reviews, string login) =>
        reviews.Single(r => r.ReviewerLogin == login);

    private static CommentFact Single(IEnumerable<CommentFact> comments, string login) =>
        comments.Single(c => c.AuthorLogin == login);

    public void Dispose()
    {
        foreach (var response in _responses)
        {
            response.Dispose();
        }

        foreach (var httpClient in _httpClients)
        {
            httpClient.Dispose();
        }
    }
}
