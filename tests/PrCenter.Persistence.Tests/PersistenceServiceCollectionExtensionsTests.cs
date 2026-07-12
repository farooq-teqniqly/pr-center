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
    public void AddPersistenceAdapter_Development_EnablesSensitiveDataLoggingAndDetailedErrors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPersistenceAdapter("Data Source=test.db", isDevelopment: true);

        // Act
        var coreOptions = ResolveCoreOptions(services);

        // Assert
        Assert.True(coreOptions.IsSensitiveDataLoggingEnabled);
        Assert.True(coreOptions.DetailedErrorsEnabled);
    }

    [Fact]
    public void AddPersistenceAdapter_NonDevelopment_DisablesSensitiveDataLoggingAndDetailedErrors()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPersistenceAdapter("Data Source=test.db", isDevelopment: false);

        // Act
        var coreOptions = ResolveCoreOptions(services);

        // Assert
        Assert.False(coreOptions.IsSensitiveDataLoggingEnabled);
        Assert.False(coreOptions.DetailedErrorsEnabled);
    }

    [Fact]
    public void AddPersistenceAdapter_KeyHolder_IsSharedAcrossResolutions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddPersistenceAdapter("Data Source=test.db", isDevelopment: false);
        using var provider = services.BuildServiceProvider();

        // Act
        var first = provider.GetRequiredService<VaultKeyHolder>();
        first.SetKey([5, 6, 7]);
        using var scope = provider.CreateScope();
        var second = scope.ServiceProvider.GetRequiredService<VaultKeyHolder>();

        // Assert
        Assert.Equal(new byte[] { 5, 6, 7 }, second.GetKeyOrThrow());
    }

    private static CoreOptionsExtension ResolveCoreOptions(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DbContextOptions<PrCenterDbContext>>();
        var coreExtension = options.FindExtension<CoreOptionsExtension>();
        Assert.NotNull(coreExtension);
        return coreExtension;
    }
}
