namespace PrCenter.Core.Diagnostics;

/// <summary>
/// The poll-level half of a diagnostics record: which poll, when it ran, how it
/// left, which owners it was configured to cover, and how many items it
/// published. Immutable data carrier with no behavior.
/// </summary>
public sealed record PollRunDiagnostics
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PollRunDiagnostics"/> class.
    /// </summary>
    /// <param name="pollId">The identifier correlating this poll across sinks.</param>
    /// <param name="startedAt">When the refresh began.</param>
    /// <param name="completedAt">When the refresh left, by any exit path.</param>
    /// <param name="outcome">How the refresh left.</param>
    /// <param name="configuredOwners">The owners the refresh was configured to cover, or null.</param>
    /// <param name="publishedCount">The items the published snapshot carried, or null when nothing was published.</param>
    public PollRunDiagnostics(
        Guid pollId,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        PollOutcome outcome,
        IReadOnlyList<string>? configuredOwners = null,
        int? publishedCount = null
    )
    {
        PollId = pollId;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Outcome = outcome;
        ConfiguredOwners = configuredOwners is null
            ? null
            : Array.AsReadOnly(configuredOwners.ToArray());
        PublishedCount = publishedCount;
    }

    /// <summary>
    /// Gets the identifier correlating this poll across sinks. A durable
    /// correlator rather than a storage row id, so it stays meaningful once it
    /// leaves the machine as a trace attribute.
    /// </summary>
    public Guid PollId { get; }

    /// <summary>Gets when the refresh began.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets when the refresh left, by any exit path.</summary>
    public DateTimeOffset CompletedAt { get; }

    /// <summary>Gets how the refresh left.</summary>
    public PollOutcome Outcome { get; }

    /// <summary>
    /// Gets the owners the refresh was configured to cover, captured from the
    /// owner enumeration itself and never assembled from the owner rows. The
    /// rows come out of the machinery a reader consults this record to debug, so
    /// a record whose only account of the configured owners were those rows
    /// could not expose a fault in producing them -- the invariant would be
    /// checked against itself.
    /// </summary>
    /// <value>
    /// <see langword="null"/> when the enumeration never completed, so nothing is
    /// known about which owners there were; an empty list when the enumeration
    /// succeeded and no owners are configured, which is a real configuration and
    /// must not read as a broken vault.
    /// </value>
    public IReadOnlyList<string>? ConfiguredOwners { get; }

    /// <summary>
    /// Gets the items the published snapshot carried, or <see langword="null"/>
    /// when the refresh published nothing. This is the cross-owner duplicate
    /// signal: each owner's own derived count can be individually correct while
    /// the owner rows sum above what was published, and that difference is not a
    /// per-owner fact.
    /// </summary>
    public int? PublishedCount { get; }
}
