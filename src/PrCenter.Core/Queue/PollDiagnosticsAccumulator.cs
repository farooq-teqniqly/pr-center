using PrCenter.Core.Diagnostics;
using PrCenter.Core.Facts;
using PrCenter.Core.Ports;

namespace PrCenter.Core.Queue;

/// <summary>
/// Collects one refresh's diagnostics rows and builds the record written to the
/// sinks. The configured owners are handed in at construction, captured from the
/// owner enumeration rather than assembled from the rows recorded afterwards --
/// this is what lets <c>rows != configured owners</c> be a detectable defect
/// rather than an invariant checked against itself. Refresh-scoped machinery
/// owned by <see cref="RefreshQueue"/>: it is fed non-null values from that one
/// call site, so it takes no null guards.
/// </summary>
internal sealed class PollDiagnosticsAccumulator
{
    private readonly Guid _pollId;
    private readonly DateTimeOffset _startedAt;
    private readonly IReadOnlyList<string> _configuredOwners;

    // Keyed case-insensitively because GitHub owner logins are, so a case
    // difference between the enumeration and a recorded row is the same owner and
    // must not read as a disagreement.
    private readonly Dictionary<string, OwnerPollDiagnostics> _rows = new(
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// Initializes a new instance of the <see cref="PollDiagnosticsAccumulator"/> class.
    /// </summary>
    /// <param name="pollId">The identifier correlating this poll across sinks.</param>
    /// <param name="startedAt">When the refresh began.</param>
    /// <param name="configuredOwners">The owners the owner enumeration returned.</param>
    public PollDiagnosticsAccumulator(
        Guid pollId,
        DateTimeOffset startedAt,
        IReadOnlyList<string> configuredOwners
    )
    {
        _pollId = pollId;
        _startedAt = startedAt;
        _configuredOwners = configuredOwners;
    }

    /// <summary>
    /// Records an owner that was fetched and derived in this refresh.
    /// </summary>
    /// <param name="window">Which owner, and when the refresh worked on it.</param>
    /// <param name="outcome">The owner's fetch outcome.</param>
    /// <param name="counts">What the fetch counted.</param>
    /// <param name="exclusions">How many pull requests each exclusion reason hid.</param>
    /// <param name="rateLimit">The rate-limit reading, or null when the response carried none.</param>
    /// <param name="contributed">The pull requests this owner contributed to the poll.</param>
    public void RecordPolled(
        OwnerPollWindow window,
        OwnerPollOutcome outcome,
        FetchCounts counts,
        ExclusionCounts exclusions,
        RateLimitReading? rateLimit,
        IReadOnlyList<PullRequestIdentity> contributed
    ) =>
        _rows[window.Owner] = new OwnerPollDiagnostics(
            window,
            outcome,
            counts,
            exclusions,
            rateLimit,
            ContributedPullRequests.For(window.Owner, contributed)
        );

    /// <summary>
    /// Records an owner whose fetch failed, so its rows were carried forward from
    /// the previous snapshot. The fetch counts read as absent and no exclusions
    /// are tallied, because no derivation ran -- the carry-over count is the only
    /// count with meaning here.
    /// </summary>
    /// <param name="window">Which owner, and when the refresh worked on it.</param>
    /// <param name="outcome">The owner's fetch outcome.</param>
    /// <param name="carriedOverCount">The rows carried forward from the previous snapshot.</param>
    /// <param name="contributed">The carried-over pull requests.</param>
    public void RecordCarriedOver(
        OwnerPollWindow window,
        OwnerPollOutcome outcome,
        int carriedOverCount,
        IReadOnlyList<PullRequestIdentity> contributed
    ) =>
        _rows[window.Owner] = new OwnerPollDiagnostics(
            window,
            outcome,
            FetchCounts.NothingFetched(carriedOverCount),
            exclusions: null,
            rateLimit: null,
            ContributedPullRequests.For(window.Owner, contributed)
        );

    /// <summary>
    /// Gives every configured owner the refresh never reached a
    /// <see cref="OwnerFetchStatus.NotPolled"/> row, so each poll ends with one
    /// row per configured owner and the abort point reads off the rows directly.
    /// An unreached owner's instants and counts are absent, never zero: nothing
    /// was attempted, which is a different claim from an empty result.
    /// </summary>
    public void MarkRemainingUnreached()
    {
        foreach (var owner in _configuredOwners)
        {
            if (_rows.ContainsKey(owner))
            {
                continue;
            }

            _rows[owner] = new OwnerPollDiagnostics(
                new OwnerPollWindow(owner),
                new OwnerPollOutcome(OwnerFetchStatus.NotPolled),
                counts: null,
                exclusions: null,
                rateLimit: null,
                ContributedPullRequests.None
            );
        }
    }

    /// <summary>
    /// Builds the record for this poll.
    /// </summary>
    /// <param name="completedAt">When the refresh left, by whichever exit path.</param>
    /// <param name="outcome">How the refresh left.</param>
    /// <param name="publishedCount">
    /// The items the published snapshot carried, or null when the refresh
    /// published nothing.
    /// </param>
    /// <returns>The poll's diagnostics record.</returns>
    public PollDiagnostics Build(
        DateTimeOffset completedAt,
        PollOutcome outcome,
        int? publishedCount
    ) =>
        new(
            new PollRunDiagnostics(
                _pollId,
                _startedAt,
                completedAt,
                outcome,
                _configuredOwners,
                publishedCount
            ),
            OrderedRows()
        );

    // Configured order first, so the rows read in the order the refresh covered
    // them and the abort point is where NotPolled starts. A row for an owner that
    // was never configured follows rather than being dropped: that disagreement is
    // exactly the defect these rows exist to expose.
    private IReadOnlyList<OwnerPollDiagnostics> OrderedRows()
    {
        var ordered = new List<OwnerPollDiagnostics>(_rows.Count);
        var configured = new HashSet<string>(_configuredOwners, StringComparer.OrdinalIgnoreCase);

        foreach (var owner in _configuredOwners)
        {
            if (_rows.TryGetValue(owner, out var row))
            {
                ordered.Add(row);
            }
        }

        ordered.AddRange(
            _rows.Where(row => !configured.Contains(row.Key)).Select(row => row.Value)
        );

        return ordered;
    }
}
