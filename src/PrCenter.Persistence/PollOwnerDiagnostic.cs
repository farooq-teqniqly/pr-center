namespace PrCenter.Persistence;

/// <summary>
/// One owner's row within a recorded poll. Every count is nullable, and the
/// nulls carry meaning: a null is "never asked", a zero is "asked and the answer
/// was none". Collapsing the two would throw away the distinction the whole
/// record exists to preserve.
/// </summary>
internal sealed class PollOwnerDiagnostic
{
    /// <summary>Gets or sets the autoincrement key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the foreign key to the owning <see cref="PollRun"/>.</summary>
    public long PollRunId { get; set; }

    /// <summary>Gets or sets the poll this row belongs to.</summary>
    public PollRun PollRun { get; set; } = null!;

    /// <summary>Gets or sets the GitHub owner (org or account) this row is for.</summary>
    public string Owner { get; set; } = null!;

    /// <summary>
    /// Gets or sets the login this owner's token resolved to, or null when
    /// resolution never completed. The token itself is never stored here.
    /// </summary>
    public string? ResolvedLogin { get; set; }

    /// <summary>
    /// Gets or sets when the refresh began this owner.
    /// </summary>
    /// <value>Null when the refresh never reached the owner -- nothing was attempted.</value>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets when the refresh finished this owner, or null as above.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets this owner's fetch status, as the status's name.</summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Gets or sets the system-composed detail behind a non-Ok status, or null.
    /// Never raw GitHub payload.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Gets or sets the nodes the review-requested search returned before
    /// deduplication.
    /// </summary>
    /// <value>
    /// Null when no search ran: the owner failed before fetching, was never
    /// reached, or the adapter reported no per-search counts.
    /// </value>
    public int? RequestedCount { get; set; }

    /// <summary>Gets or sets the reviewed-by search's node count, or null as above.</summary>
    public int? ReviewedCount { get; set; }

    /// <summary>
    /// Gets or sets the distinct pull requests the two searches unioned to.
    /// </summary>
    /// <value>Null when nothing was fetched.</value>
    public int? UnionCount { get; set; }

    /// <summary>
    /// Gets or sets the pull requests that survived derivation into shown items.
    /// </summary>
    /// <value>Null when nothing was fetched, and so nothing was derived.</value>
    public int? DerivedCount { get; set; }

    /// <summary>
    /// Gets or sets the rows this owner carried forward from the previous snapshot
    /// because its fetch failed.
    /// </summary>
    /// <value>
    /// Null when the refresh never reached the owner. Zero means the owner failed
    /// and had nothing to carry -- it has never been fresh -- which is the one count
    /// with meaning on a failed owner, so it must not read as absent.
    /// </value>
    public int? CarriedOverCount { get; set; }

    /// <summary>Gets or sets how many pull requests were hidden as drafts.</summary>
    /// <value>Null when no derivation ran; the four exclusion counts are null together.</value>
    public int? DraftExclusions { get; set; }

    /// <summary>Gets or sets how many were hidden as closed or merged, or null as above.</summary>
    public int? ClosedOrMergedExclusions { get; set; }

    /// <summary>Gets or sets how many were hidden as already approved by the user, or null as above.</summary>
    public int? ApprovedExclusions { get; set; }

    /// <summary>Gets or sets how many were hidden as neither requested nor ever reviewed, or null as above.</summary>
    public int? UntrackedExclusions { get; set; }

    /// <summary>Gets or sets the point allowance left after this owner's query.</summary>
    /// <value>Null when there was no successful fetch, or the response carried no rate limit.</value>
    public int? RateLimitRemaining { get; set; }

    /// <summary>Gets or sets when the rate-limit window resets, or null as above.</summary>
    public DateTimeOffset? RateLimitResetAt { get; set; }

    /// <summary>
    /// Gets or sets the points this owner's query consumed, or null as above.
    /// Stored though the diagnostics view does not render it: it is the number
    /// that predicts when the limit will bite, and it cannot be backfilled later.
    /// </summary>
    public int? RateLimitCost { get; set; }

    /// <summary>
    /// Gets or sets the <c>owner/repo#number</c> identifiers this owner
    /// contributed. Titles and bodies are never stored; the identifier is the
    /// whole content.
    /// </summary>
    public IReadOnlyList<string> PullRequestIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many of <see cref="PullRequestIds"/> belong to a different
    /// owner. Stored rather than recomputed at read time so a telemetry sink can
    /// emit it without re-parsing identifiers. A non-zero value is normal, not a
    /// fault -- it attributes cross-owner overlap to the token reaching across.
    /// </summary>
    public int ForeignItemCount { get; set; }
}
