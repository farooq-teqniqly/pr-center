using Microsoft.EntityFrameworkCore;

namespace PrCenter.Persistence;

/// <summary>
/// EF Core context for PR-Center's local SQLite state: the encrypted owner
/// tokens, the single app-security row, the single app-settings row, and the
/// bounded ring of recorded polls with their per-owner diagnostics rows.
/// </summary>
internal sealed class PrCenterDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrCenterDbContext"/> class.
    /// </summary>
    /// <param name="options">The options configured by the composition root.</param>
    public PrCenterDbContext(DbContextOptions<PrCenterDbContext> options)
        : base(options) { }

    /// <summary>Gets the encrypted owner tokens, one per GitHub owner.</summary>
    public DbSet<OwnerToken> OwnerTokens => Set<OwnerToken>();

    /// <summary>Gets the single app-security row establishing the vault.</summary>
    public DbSet<AppSecurity> AppSecurity => Set<AppSecurity>();

    /// <summary>Gets the single app-settings row holding the poll interval.</summary>
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    /// <summary>Gets the recorded polls, newest last by key.</summary>
    public DbSet<PollRun> PollRuns => Set<PollRun>();

    /// <summary>Gets the per-owner diagnostics rows, one per configured owner per poll.</summary>
    public DbSet<PollOwnerDiagnostic> PollOwnerDiagnostics => Set<PollOwnerDiagnostic>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OwnerToken>(token =>
        {
            token.HasKey(entity => entity.Owner);
            token.Property(entity => entity.Owner).IsRequired().HasMaxLength(255);
            token.Property(entity => entity.Nonce).IsRequired();
            token.Property(entity => entity.Ciphertext).IsRequired();
            token.Property(entity => entity.Tag).IsRequired();
            token.Property(entity => entity.SavedAt);
        });

        modelBuilder.Entity<AppSetting>(setting =>
        {
            setting.HasKey(entity => entity.Id);

            // Single-row table, same pattern as AppSecurity: the id is always 1
            // and assigned explicitly, so "a row exists" means "an interval has
            // been stored" and its absence means the default.
            setting.Property(entity => entity.Id).ValueGeneratedNever();
            setting.Property(entity => entity.PollIntervalSeconds).IsRequired();
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

        modelBuilder.Entity<PollRun>(run =>
        {
            run.HasKey(entity => entity.Id);

            // Generated, unlike the single-row tables: the key both orders the
            // retention trim and identifies a row to cascade from.
            run.Property(entity => entity.Id).ValueGeneratedOnAdd();
            run.HasIndex(entity => entity.PollId).IsUnique();
            run.Property(entity => entity.StartedAt).IsRequired();
            run.Property(entity => entity.CompletedAt).IsRequired();
            run.Property(entity => entity.Outcome).IsRequired().HasMaxLength(32);

            // Nullable through the converter, so a poll whose owner enumeration
            // never completed stores SQL NULL rather than an empty list.
            run.Property(entity => entity.ConfiguredOwners)
                .HasConversion(DelimitedStringList.NullableConverter, DelimitedStringList.Comparer);

            run.HasMany(entity => entity.Owners)
                .WithOne(owner => owner.PollRun)
                .HasForeignKey(owner => owner.PollRunId)
                // Retention deletes whole polls; the children go with the parent
                // rather than being trimmed by a second pass that could leave a
                // partial poll behind.
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PollOwnerDiagnostic>(owner =>
        {
            owner.HasKey(entity => entity.Id);
            owner.Property(entity => entity.Id).ValueGeneratedOnAdd();
            owner.Property(entity => entity.Owner).IsRequired().HasMaxLength(255);
            owner.Property(entity => entity.ResolvedLogin).HasMaxLength(255);
            owner.Property(entity => entity.Status).IsRequired().HasMaxLength(32);

            owner
                .Property(entity => entity.PullRequestIds)
                .IsRequired()
                .HasConversion(DelimitedStringList.Converter, DelimitedStringList.Comparer);
        });
    }
}
