using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Web.Polling;

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

    [Fact]
    public void Host_WhenBuilt_ResolvesRefreshQueueUseCase()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act
        var resolved = scope.ServiceProvider.GetRequiredService<IRefreshQueue>();

        // Assert
        Assert.IsType<RefreshQueue>(resolved);
    }

    [Fact]
    public void Host_WhenBuilt_ResolvesQueueSnapshotHolderAsSingleton()
    {
        // Arrange
        using var first = _factory.Services.CreateScope();
        using var second = _factory.Services.CreateScope();

        // Act
        var fromFirst = first.ServiceProvider.GetRequiredService<QueueSnapshotHolder>();
        var fromSecond = second.ServiceProvider.GetRequiredService<QueueSnapshotHolder>();

        // Assert
        Assert.Same(fromFirst, fromSecond);
    }

    [Fact]
    public void Host_WhenBuilt_ResolvesRefreshTriggerAndInterfaceToTheSameSingleton()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act
        var concrete = scope.ServiceProvider.GetRequiredService<RefreshTrigger>();
        var asInterface = scope.ServiceProvider.GetRequiredService<IRefreshTrigger>();

        // Assert
        Assert.Same(concrete, asInterface);
    }

    [Fact]
    public void Host_WhenBuilt_BindsPollIntervalFromConfiguration()
    {
        // Arrange / Act
        var options = _factory.Services.GetRequiredService<IOptions<PollingOptions>>();

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), options.Value.Interval);
    }

    [Fact]
    public void Host_WhenBuilt_RegistersQueuePollingServiceAsHostedService()
    {
        // Arrange / Act
        var hostedServices = _factory.Services.GetServices<IHostedService>();

        // Assert
        Assert.Contains(hostedServices, service => service is QueuePollingService);
    }
}
