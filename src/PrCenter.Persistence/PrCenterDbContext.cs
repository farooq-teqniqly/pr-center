using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence;

/// <summary>
/// EF Core context for PR-Center's local SQLite state. Holds the last-seen
/// markers; token and settings schema arrive with their own changes.
/// </summary>
internal sealed class PrCenterDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrCenterDbContext"/> class.
    /// </summary>
    /// <param name="options">The options configured by the composition root.</param>
    public PrCenterDbContext(DbContextOptions<PrCenterDbContext> options)
        : base(options) { }

    /// <summary>Gets the last-seen markers, one per pull request.</summary>
    public DbSet<LastSeenMarker> LastSeenMarkers => Set<LastSeenMarker>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<LastSeenMarker>(marker =>
        {
            marker.HasKey(entity => entity.PullRequestId);

            // The id is a GitHub GraphQL node id (well under 100 chars); 255 is
            // an ample cap. SQLite does not enforce column length, so this is
            // model/migration metadata rather than a runtime constraint.
            marker.Property(entity => entity.PullRequestId).IsRequired().HasMaxLength(255);
        });
    }
}
