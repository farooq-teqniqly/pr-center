using System.Net;
using NSubstitute;
using PrCenter.Core.Ports;

namespace PrCenter.GitHub.Tests;

public sealed class GetPullRequestFactsAsyncTests : IDisposable
{
    private readonly GitHubClientHarness _harness = new();

    [Fact]
    public async Task GetPullRequestFactsAsync_WhenMerged_ReturnsFactsWithClosedOrMerged()
    {
        // Arrange
        var client = _harness.Build(
            GitHubClientHarness.Ok(GraphQlFixtures.MergedPullRequestResponse)
        );

        // Act
        var facts = await client.GetPullRequestFactsAsync(
            GraphQlFixtures.Owner,
            "repo",
            9,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(facts);
        Assert.True(facts.Status.IsClosedOrMerged);
    }

    [Fact]
    public async Task GetPullRequestFactsAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var client = _harness.Build(
            GitHubClientHarness.Ok(GraphQlFixtures.MissingPullRequestResponse)
        );

        // Act
        var facts = await client.GetPullRequestFactsAsync(
            GraphQlFixtures.Owner,
            "repo",
            9,
            CancellationToken.None
        );

        // Assert
        Assert.Null(facts);
    }

    [Fact]
    public async Task GetPullRequestFactsAsync_WhenTokenMissing_ReturnsNull()
    {
        // Arrange
        var client = _harness.Build(
            GitHubClientHarness.Ok(GraphQlFixtures.MergedPullRequestResponse),
            token: null
        );

        // Act
        var facts = await client.GetPullRequestFactsAsync(
            GraphQlFixtures.Owner,
            "repo",
            9,
            CancellationToken.None
        );

        // Assert
        Assert.Null(facts);
    }

    [Fact]
    public async Task GetPullRequestFactsAsync_WhenServerFails_ReturnsNull()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var client = _harness.Build(response);

        // Act
        var facts = await client.GetPullRequestFactsAsync(
            GraphQlFixtures.Owner,
            "repo",
            9,
            CancellationToken.None
        );

        // Assert
        Assert.Null(facts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetPullRequestFactsAsync_WithMissingOwner_Throws(string? owner)
    {
        // Arrange
        var client = _harness.Build(
            GitHubClientHarness.Ok(GraphQlFixtures.MissingPullRequestResponse)
        );

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            client.GetPullRequestFactsAsync(owner!, "repo", 9, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetPullRequestFactsAsync_WithMissingRepository_Throws(string? repository)
    {
        // Arrange
        var client = _harness.Build(
            GitHubClientHarness.Ok(GraphQlFixtures.MissingPullRequestResponse)
        );

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            client.GetPullRequestFactsAsync(
                GraphQlFixtures.Owner,
                repository!,
                9,
                CancellationToken.None
            )
        );
    }

    public void Dispose() => _harness.Dispose();
}
