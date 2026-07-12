using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence;

/// <summary>
/// EF Core context for PR-Center's local SQLite state. Holds the last-seen
/// markers, the encrypted owner tokens, and the single app-security row;
/// settings schema arrives with its own change.
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

    /// <summary>Gets the encrypted owner tokens, one per GitHub owner.</summary>
    public DbSet<OwnerToken> OwnerTokens => Set<OwnerToken>();

    /// <summary>Gets the single app-security row establishing the vault.</summary>
    public DbSet<AppSecurity> AppSecurity => Set<AppSecurity>();

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

        modelBuilder.Entity<OwnerToken>(token =>
        {
            token.HasKey(entity => entity.Owner);
            token.Property(entity => entity.Owner).IsRequired().HasMaxLength(255);
            token.Property(entity => entity.Nonce).IsRequired();
            token.Property(entity => entity.Ciphertext).IsRequired();
            token.Property(entity => entity.Tag).IsRequired();
        });

        modelBuilder.Entity<AppSecurity>(security =>
        {
            security.HasKey(entity => entity.Id);

            // Single-row table: the id is assigned explicitly (always 1), not
            // generated, so "a row exists" is the vault's initialized discriminator.
            security.Property(entity => entity.Id).ValueGeneratedNever();
            security.Property(entity => entity.Salt).IsRequired();
            security.Property(entity => entity.SentinelNonce).IsRequired();
            security.Property(entity => entity.SentinelCiphertext).IsRequired();
            security.Property(entity => entity.SentinelTag).IsRequired();
        });
    }
}
