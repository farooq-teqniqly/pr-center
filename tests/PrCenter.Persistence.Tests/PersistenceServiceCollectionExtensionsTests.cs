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
            services.AddPersistenceAdapter("Data Source=test.db")
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
            services.AddPersistenceAdapter(connectionString!)
        );
    }
}
