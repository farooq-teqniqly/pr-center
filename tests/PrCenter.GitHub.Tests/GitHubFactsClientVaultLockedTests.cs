using PrCenter.Core.Locking;

namespace PrCenter.GitHub.Tests;

public sealed class GitHubFactsClientVaultLockedTests
{
    [Fact]
    public async Task GetReviewQueueFactsAsync_WhenVaultLocked_PropagatesVaultLockedException()
    {
        // Arrange
        using var harness = new GitHubClientHarness();
        var client = harness.BuildWithLockedVault();

        // Act / Assert
        await Assert.ThrowsAsync<VaultLockedException>(() =>
            client.GetReviewQueueFactsAsync(GraphQlFixtures.Owner, "me", CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetPullRequestFactsAsync_WhenVaultLocked_PropagatesVaultLockedException()
    {
        // Arrange
        using var harness = new GitHubClientHarness();
        var client = harness.BuildWithLockedVault();

        // Act / Assert
        await Assert.ThrowsAsync<VaultLockedException>(() =>
            client.GetPullRequestFactsAsync(
                GraphQlFixtures.Owner,
                "repo",
                1,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task GetAuthenticatedUserLoginAsync_WhenVaultLocked_PropagatesVaultLockedException()
    {
        // Arrange
        using var harness = new GitHubClientHarness();
        var client = harness.BuildWithLockedVault();

        // Act / Assert
        await Assert.ThrowsAsync<VaultLockedException>(() =>
            client.GetAuthenticatedUserLoginAsync(GraphQlFixtures.Owner, CancellationToken.None)
        );
    }
}
