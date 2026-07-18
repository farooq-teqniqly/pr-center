using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence.Tests;

public sealed class SqliteTestDatabaseTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    [Fact]
    public async Task MigratedDatabase_PersistsAndReadsBackAnOwnerToken()
    {
        // Arrange
        await using (var write = _database.CreateContext())
        {
            write.OwnerTokens.Add(
                new OwnerToken
                {
                    Owner = "PerfectServe",
                    Nonce = [1],
                    Ciphertext = [2, 3],
                    Tag = [4],
                }
            );
            await write.SaveChangesAsync(CancellationToken.None);
        }

        // Act
        await using var read = _database.CreateContext();
        var token = await read
            .OwnerTokens.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Owner == "PerfectServe", CancellationToken.None);

        // Assert
        Assert.NotNull(token);
        Assert.Equal<byte[]>([2, 3], token.Ciphertext);
    }

    public void Dispose() => _database.Dispose();
}
