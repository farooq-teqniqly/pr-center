using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PrCenter.Persistence.Tests;

public sealed class PersistenceMigrationExtensionsTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"prcenter-migrate-{Guid.NewGuid():N}.db"
    );

    [Fact]
    public async Task MigratePersistenceAsync_SchemaLessFile_CreatesTokenTableForRoundTrip()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistenceAdapter($"Data Source={_path};Pooling=False", isDevelopment: false);
        await using var provider = services.BuildServiceProvider();

        // Act
        await provider.MigratePersistenceAsync(CancellationToken.None);

        // Assert
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PrCenterDbContext>();
        context.OwnerTokens.Add(
            new OwnerToken
            {
                Owner = "PerfectServe",
                Nonce = [1],
                Ciphertext = [2, 3],
                Tag = [4],
            }
        );
        await context.SaveChangesAsync(CancellationToken.None);
        var read = await context
            .OwnerTokens.AsNoTracking()
            .SingleAsync(token => token.Owner == "PerfectServe", CancellationToken.None);
        Assert.Equal<byte[]>([2, 3], read.Ciphertext);
    }

    public void Dispose()
    {
        foreach (var suffix in (ReadOnlySpan<string>)["", "-wal", "-shm"])
        {
            var file = _path + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
