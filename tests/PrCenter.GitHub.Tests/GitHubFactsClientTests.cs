using PrCenter.GitHub;

namespace PrCenter.GitHub.Tests;

public sealed class GitHubFactsClientTests
{
    [Fact]
    public async Task GetAuthenticatedUserLoginAsync_WhenCalled_ThrowsNotImplemented()
    {
        // Arrange
        // The stub throws before touching its dependencies, so they are unused.
        var client = new GitHubFactsClient(null!, null!);

        // Act / Assert
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            client.GetAuthenticatedUserLoginAsync("owner", CancellationToken.None)
        );
    }
}
