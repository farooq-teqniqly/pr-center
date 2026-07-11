using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using PrCenter.Persistence;

namespace PrCenter.Persistence.Tests;

public sealed class PersistenceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPersistenceAdapter_NullServices_Throws()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() =>
            services.AddPersistenceAdapter("Data Source=test.db", isDevelopment: false)
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddPersistenceAdapter_NullOrWhitespaceConnectionString_Throws(
        string? connectionString
    )
    {
        // Arrange
        var services = new ServiceCollection();

        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            services.AddPersistenceAdapter(connectionString!, isDevelopment: false)
        );
    }

    [Fact]
    public void AddPersistenceAdapter_Development_EnablesSensitiveDataLogging()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPersistenceAdapter("Data Source=test.db", isDevelopment: true);

        // Act
        var enabled = ResolveSensitiveDataLogging(services);

        // Assert
        Assert.True(enabled);
    }

    [Fact]
    public void AddPersistenceAdapter_NonDevelopment_DisablesSensitiveDataLogging()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPersistenceAdapter("Data Source=test.db", isDevelopment: false);

        // Act
        var enabled = ResolveSensitiveDataLogging(services);

        // Assert
        Assert.False(enabled);
    }

    private static bool ResolveSensitiveDataLogging(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DbContextOptions<PrCenterDbContext>>();
        var coreExtension = options.FindExtension<CoreOptionsExtension>();
        Assert.NotNull(coreExtension);
        return coreExtension.IsSensitiveDataLoggingEnabled;
    }
}
