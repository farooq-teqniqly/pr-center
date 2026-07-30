namespace PrCenter.Core.Diagnostics;

using PrCenter.Core.Ports;

/// <summary>
/// One owner's row in a poll's diagnostics record. Every configured owner gets
/// exactly one row, including the owners a refresh never reached -- an absence
/// and a <see cref="OwnerFetchStatus.NotPolled"/> row are different claims, and
/// they must never collapse into each other.
/// </summary>
public sealed record OwnerPollDiagnostics
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerPollDiagnostics"/> class.
    /// </summary>
    /// <param name="window">Which owner the row is for and when the refresh worked on it.</param>
    /// <param name="outcome">How the owner's fetch turned out.</param>
    /// <param name="counts">
    /// What the fetch counted, or null when the refresh never reached this owner.
    /// </param>
    /// <param name="exclusions">
    /// How many pull requests each exclusion reason hid, or null when no
    /// derivation ran (a failed or unreached owner).
    /// </param>
    /// <param name="rateLimit">
    /// The rate-limit reading for the fetch, or null when there was no successful
    /// fetch or the response did not carry one.
    /// </param>
    /// <param name="contributed">The pull requests this owner contributed, and their foreign count.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="window"/>, <paramref name="outcome"/>, or
    /// <paramref name="contributed"/> is null.
    /// </exception>
    public OwnerPollDiagnostics(
        OwnerPollWindow window,
        OwnerPollOutcome outcome,
        FetchCounts? counts,
        ExclusionCounts? exclusions,
        RateLimitReading? rateLimit,
        ContributedPullRequests contributed
    )
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(contributed);

        Window = window;
        Outcome = outcome;
        Counts = counts;
        Exclusions = exclusions;
        RateLimit = rateLimit;
        Contributed = contributed;
    }

    /// <summary>Gets which owner the row is for and when the refresh worked on it.</summary>
    public OwnerPollWindow Window { get; }

    /// <summary>Gets how the owner's fetch turned out.</summary>
    public OwnerPollOutcome Outcome { get; }

    /// <summary>Gets what the fetch counted, or null when the refresh never reached this owner.</summary>
    public FetchCounts? Counts { get; }

    /// <summary>Gets how many pull requests each exclusion reason hid, or null when no derivation ran.</summary>
    public ExclusionCounts? Exclusions { get; }

    /// <summary>Gets the rate-limit reading for the fetch, or null when there is none.</summary>
    public RateLimitReading? RateLimit { get; }

    /// <summary>Gets the pull requests this owner contributed, and their foreign count.</summary>
    public ContributedPullRequests Contributed { get; }
}
