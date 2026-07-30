namespace PrCenter.Persistence;

/// <summary>
/// One recorded poll: when it ran, how it left, which owners it was configured
/// to cover, and how many items it published. Parent of one
/// <see cref="PollOwnerDiagnostic"/> per configured owner, so retention can
/// evict whole polls with a cascade rather than a subquery over a denormalized
/// column.
/// </summary>
internal sealed class PollRun
{
    /// <summary>
    /// Gets or sets the autoincrement key. Orders naturally for the retention
    /// trim and cascades to the owner rows; it is a local row identifier and
    /// means nothing outside this file.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the durable correlator for this poll, unique across rows.
    /// Distinct from <see cref="Id"/> because this is the value that leaves the
    /// machine as a trace attribute, where a local rowid would be meaningless.
    /// </summary>
    public Guid PollId { get; set; }

    /// <summary>Gets or sets when the refresh began.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets when the refresh left, by whichever exit path.</summary>
    public DateTimeOffset CompletedAt { get; set; }

    /// <summary>Gets or sets how the refresh left, as the outcome's name.</summary>
    public string Outcome { get; set; } = null!;

    /// <summary>
    /// Gets or sets the owners the refresh was configured to cover, captured from
    /// the owner enumeration rather than assembled from the owner rows, so
    /// <c>rows != configured owners</c> is a detectable defect.
    /// </summary>
    /// <value>
    /// Null when the enumeration never completed, so nothing is known about which
    /// owners there were; empty when the enumeration succeeded and no owners are
    /// configured. Those are different claims and the column must keep them apart.
    /// </value>
    public IReadOnlyList<string>? ConfiguredOwners { get; set; }

    /// <summary>
    /// Gets or sets how many owners were configured, stored alongside
    /// <see cref="ConfiguredOwners"/> so a reader can order and filter on it
    /// without deserializing the list.
    /// </summary>
    /// <value>
    /// Nullable for the same reason as <see cref="ConfiguredOwners"/>: zero is a
    /// real configuration, so writing zero when the owner list could not be read
    /// would render a broken vault as an empty one.
    /// </value>
    public int? OwnerCount { get; set; }

    /// <summary>
    /// Gets or sets how many items the published snapshot carried.
    /// </summary>
    /// <value>
    /// Null when the refresh published nothing, which is every aborted, canceled,
    /// and faulted poll. Zero means a snapshot was published and was empty.
    /// </value>
    public int? PublishedCount { get; set; }

    /// <summary>Gets or sets this poll's owner rows.</summary>
    public List<PollOwnerDiagnostic> Owners { get; set; } = [];
}
