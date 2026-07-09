using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence;

/// <summary>
/// EF Core context for PR-Center's local SQLite state. Deliberately empty:
/// entities, schema, and migrations arrive with the changes that specify the
/// state and token-vault behavior.
/// </summary>
internal sealed class PrCenterDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrCenterDbContext"/> class.
    /// </summary>
    /// <param name="options">The options configured by the composition root.</param>
    public PrCenterDbContext(DbContextOptions<PrCenterDbContext> options)
        : base(options) { }
}
