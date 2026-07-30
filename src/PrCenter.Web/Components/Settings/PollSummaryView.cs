using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Web.Components.Settings;

/// <summary>
/// Pure projection of one recorded poll into the facts the summary line shows
/// without expanding it: how many of its configured owners it actually reached,
/// whether its owner rows disagree with the owners it was configured to cover,
/// and whether its owner rows derived more items than the poll published.
/// Reads only; the diagnostics record is never written from here.
/// </summary>
internal sealed record PollSummaryView
{
    private PollSummaryView(
        PollDiagnostics poll,
        int polledOwners,
        IReadOnlyList<string> missingOwners,
        IReadOnlyList<string> unconfiguredOwners,
        int derivedTotal
    )
    {
        Poll = poll;
        PolledOwners = polledOwners;
        MissingOwners = missingOwners;
        UnconfiguredOwners = unconfiguredOwners;
        DerivedTotal = derivedTotal;
    }

    /// <summary>Gets the recorded poll this summarizes.</summary>
    public PollDiagnostics Poll { get; }

    /// <summary>Gets how many configured owners the refresh actually reached.</summary>
    public int PolledOwners { get; }

    /// <summary>Gets the configured owners that have no row, which should be none.</summary>
    public IReadOnlyList<string> MissingOwners { get; }

    /// <summary>Gets the owners with a row that were never configured, which should be none.</summary>
    public IReadOnlyList<string> UnconfiguredOwners { get; }

    /// <summary>Gets the sum of the owner rows' derived counts.</summary>
    public int DerivedTotal { get; }

    /// <summary>
    /// Gets a value indicating whether the owner rows disagree with the configured
    /// owners. Unlike the overlap mark, this is a defect: the rows and the
    /// enumeration are independent witnesses to the same fact, so a mismatch means
    /// one of them is wrong.
    /// </summary>
    public bool OwnersDisagree => MissingOwners.Count > 0 || UnconfiguredOwners.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the owner rows derived more items than the
    /// poll published, meaning at least one pull request reached the queue from
    /// more than one owner.
    /// </summary>
    /// <remarks>
    /// Legitimate rather than faulty: a token whose resource owner is one org can
    /// still see another configured owner's repositories, and the accumulator
    /// resolves that correctly. The mark explains why the counts below will not add
    /// up as expected -- a tell that reads as an alarm on a healthy install is a tell
    /// that gets ignored on a broken one.
    /// </remarks>
    public bool HasOverlap => Poll.Run.PublishedCount is { } published && DerivedTotal > published;

    /// <summary>
    /// Summarizes one recorded poll.
    /// </summary>
    /// <param name="poll">The poll to summarize.</param>
    /// <returns>The summary.</returns>
    public static PollSummaryView For(PollDiagnostics poll)
    {
        var rowOwners = poll.Owners.Select(row => row.Window.Owner).ToArray();

        // A poll whose owner enumeration never completed has nothing to disagree
        // with: the comparison needs two witnesses, and only one of them exists.
        var configured = poll.Run.ConfiguredOwners;
        var missing = configured is null ? [] : Except(configured, rowOwners);
        var unconfigured = configured is null
            ? Array.Empty<string>()
            : Except(rowOwners, configured);

        return new PollSummaryView(
            poll,
            poll.Owners.Count(row => row.Outcome.Status is not OwnerFetchStatus.NotPolled),
            missing,
            unconfigured,
            poll.Owners.Sum(row => row.Counts?.Derived ?? 0)
        );
    }

    // Owner logins are case-insensitive identifiers, so a casing difference between
    // the enumeration and a row is the same owner, not a disagreement.
    private static string[] Except(IEnumerable<string> left, IEnumerable<string> right) =>
        [.. left.Except(right, StringComparer.OrdinalIgnoreCase)];
}
