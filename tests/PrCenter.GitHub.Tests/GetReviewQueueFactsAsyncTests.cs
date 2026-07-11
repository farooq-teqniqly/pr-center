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

    private async Task<PullRequestFacts> FactsForAsync(string id)
    {
        var result = await FetchQueueAsync(GraphQlFixtures.ReviewQueueResponse);
        return result.Facts.Single(f => f.Identity.Id == id);
    }

    private async Task<OwnerFactsResult> FetchQueueAsync(string responseBody)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        };
        _responses.Add(response);

        var handler = Substitute.For<FakeHttpMessageHandler>();
        handler
            .MockSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        _httpClients.Add(httpClient);

        var vault = Substitute.For<ITokenVault>();
        vault.GetTokenAsync(GraphQlFixtures.Owner, Arg.Any<CancellationToken>()).Returns("token");

        var client = new GitHubFactsClient(httpClient, vault);
        return await client.GetReviewQueueFactsAsync(
            GraphQlFixtures.Owner,
            "octocat",
            CancellationToken.None
        );
    }

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
