namespace PrCenter.GitHub.Tests;

public sealed class GetAuthenticatedUserLoginAsyncTests : IDisposable
{
    private readonly GitHubClientHarness _harness = new();

    [Fact]
    public async Task GetAuthenticatedUserLoginAsync_ReturnsViewerLogin()
    {
        // Arrange
        var client = _harness.Build(GitHubClientHarness.Ok(GraphQlFixtures.ViewerResponse));

        // Act
        var login = await client.GetAuthenticatedUserLoginAsync(
            GraphQlFixtures.Owner,
            CancellationToken.None
        );

        // Assert
        Assert.Equal("octocat", login);
    }

    [Fact]
    public async Task GetAuthenticatedUserLoginAsync_WhenTokenMissing_Throws()
    {
        // Arrange
        var client = _harness.Build(
            GitHubClientHarness.Ok(GraphQlFixtures.ViewerResponse),
            token: null
        );

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAuthenticatedUserLoginAsync(GraphQlFixtures.Owner, CancellationToken.None)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetAuthenticatedUserLoginAsync_WithMissingOwner_Throws(string? owner)
    {
        // Arrange
        var client = _harness.Build(GitHubClientHarness.Ok(GraphQlFixtures.ViewerResponse));

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            client.GetAuthenticatedUserLoginAsync(owner!, CancellationToken.None)
        );
    }

    public void Dispose() => _harness.Dispose();
}
