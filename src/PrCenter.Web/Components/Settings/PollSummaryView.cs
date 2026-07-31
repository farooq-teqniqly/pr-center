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
        int contributedTotal
    )
    {
        Poll = poll;
        PolledOwners = polledOwners;
        MissingOwners = missingOwners;
        UnconfiguredOwners = unconfiguredOwners;
        ContributedTotal = contributedTotal;
    }

    /// <summary>Gets the recorded poll this summarizes.</summary>
    public PollDiagnostics Poll { get; }

    /// <summary>Gets how many configured owners the refresh actually reached.</summary>
    public int PolledOwners { get; }

    /// <summary>Gets the configured owners that have no row, which should be none.</summary>
    public IReadOnlyList<string> MissingOwners { get; }

    /// <summary>Gets the owners with a row that were never configured, which should be none.</summary>
    public IReadOnlyList<string> UnconfiguredOwners { get; }

    /// <summary>
    /// Gets the rows every owner contributed to the poll: what a fetched owner
    /// derived, and what a failed owner carried over from the last good snapshot,
    /// since both reach the published snapshot.
    /// </summary>
    public int ContributedTotal { get; }

    /// <summary>
    /// Gets a value indicating whether the owner rows disagree with the configured
    /// owners. Unlike the overlap mark, this is a defect: the rows and the
    /// enumeration are independent witnesses to the same fact, so a mismatch means
    /// one of them is wrong.
    /// </summary>
    public bool OwnersDisagree => MissingOwners.Count > 0 || UnconfiguredOwners.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the owner rows contributed more items than
    /// the poll published, meaning at least one pull request reached the queue from
    /// more than one owner.
    /// </summary>
    /// <remarks>
    /// Legitimate rather than faulty: a token whose resource owner is one org can
    /// still see another configured owner's repositories, and the accumulator
    /// resolves that correctly. The mark explains why the counts below will not add
    /// up as expected -- a tell that reads as an alarm on a healthy install is a tell
    /// that gets ignored on a broken one.
    /// </remarks>
    public bool HasOverlap =>
        Poll.Run.PublishedCount is { } published && ContributedTotal > published;

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
            poll.Owners.Sum(Contributed)
        );
    }

    // A failed owner's carried rows are published alongside the fresh owners' rows,
    // so they count toward the total the published count is compared against;
    // counting only what was derived would hide every overlap involving a
    // carried-over owner. An owner never reached has no counts and contributes
    // nothing.
    private static int Contributed(OwnerPollDiagnostics row) =>
        row.Counts is { } counts ? counts.Derived ?? counts.CarriedOver : 0;

    // Owner logins are case-insensitive identifiers, so a casing difference between
    // the enumeration and a row is the same owner, not a disagreement.
    private static string[] Except(IEnumerable<string> left, IEnumerable<string> right) =>
        [.. left.Except(right, StringComparer.OrdinalIgnoreCase)];
}
