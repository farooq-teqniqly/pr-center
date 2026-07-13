using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PrCenter.Core.Ports;

namespace PrCenter.Web.Tests;

public sealed class DiCompositionRootTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DiCompositionRootTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Host_WhenBuilt_ResolvesGitHubFactsToGitHubAdapter()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act
        var resolved = scope.ServiceProvider.GetRequiredService<IGitHubFacts>();

        // Assert
        Assert.Equal("PrCenter.GitHub", resolved.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void Host_WhenBuilt_ResolvesStateStoreToPersistenceAdapter()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act
        var resolved = scope.ServiceProvider.GetRequiredService<IStateStore>();

        // Assert
        Assert.Equal("PrCenter.Persistence", resolved.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void Host_WhenBuilt_ResolvesTokenVaultToPersistenceAdapter()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act
        var resolved = scope.ServiceProvider.GetRequiredService<ITokenVault>();

        // Assert
        Assert.Equal("PrCenter.Persistence", resolved.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void Host_WhenBuilt_ResolvesAppLockToPersistenceAdapter()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act
        var resolved = scope.ServiceProvider.GetRequiredService<IAppLock>();

        // Assert
        Assert.Equal("PrCenter.Persistence", resolved.GetType().Assembly.GetName().Name);
    }
}
