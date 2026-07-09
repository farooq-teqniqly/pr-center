using PrCenter.GitHub;

namespace PrCenter.GitHub.Tests;

public sealed class GitHubFactsClientTests
{
    [Fact]
    public async Task GetAuthenticatedUserLoginAsync_WhenCalled_ThrowsNotImplemented()
    {
        // Arrange
        var client = new GitHubFactsClient();

        // Act / Assert
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            client.GetAuthenticatedUserLoginAsync("owner", CancellationToken.None)
        );
    }
}
