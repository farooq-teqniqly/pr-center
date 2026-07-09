using Microsoft.Extensions.DependencyInjection;
using PrCenter.GitHub;

namespace PrCenter.GitHub.Tests;

public sealed class GitHubServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGitHubAdapter_NullServices_Throws()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => services.AddGitHubAdapter());
    }
}
