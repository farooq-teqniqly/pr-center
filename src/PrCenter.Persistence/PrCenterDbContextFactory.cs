using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PrCenter.Persistence;

/// <summary>
/// Design-time factory used only by the EF Core tooling to build a
/// <see cref="PrCenterDbContext"/> when generating migrations, since this
/// library has no application host. The connection string is a placeholder --
/// migration generation is model-based and never opens the database.
/// </summary>
internal sealed class PrCenterDbContextFactory : IDesignTimeDbContextFactory<PrCenterDbContext>
{
    /// <inheritdoc />
    public PrCenterDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PrCenterDbContext>()
            .UseSqlite("Data Source=prcenter-design.db")
            .Options;

        return new PrCenterDbContext(options);
    }
}
