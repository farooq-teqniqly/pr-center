namespace PrCenter.Core.Diagnostics;

using PrCenter.Core.Derivation;

/// <summary>
/// How many of an owner's fetched pull requests each exclusion reason hid, one
/// tally per <see cref="MembershipExclusion"/>. These are what turn
/// "union 15, derived 4" from a question into an answer: the four tallies plus
/// the derived count account for every pull request the union contained.
/// </summary>
/// <param name="Draft">Hidden as drafts, excluded even where the user is a requested reviewer.</param>
/// <param name="ClosedOrMerged">Hidden as closed or merged.</param>
/// <param name="Approved">Hidden because the user's latest review approved with no re-request pending.</param>
/// <param name="Untracked">Hidden because the user is neither requested nor has ever reviewed.</param>
public sealed record ExclusionCounts(int Draft, int ClosedOrMerged, int Approved, int Untracked)
{
    /// <summary>Gets the total pull requests hidden, across every reason.</summary>
    public int Total => Draft + ClosedOrMerged + Approved + Untracked;

    /// <summary>
    /// Tallies the exclusions a single owner's derivation produced.
    /// </summary>
    /// <param name="exclusions">One entry per hidden pull request.</param>
    /// <returns>The per-reason counts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exclusions"/> is null.</exception>
    public static ExclusionCounts Tally(IEnumerable<MembershipExclusion> exclusions)
    {
        ArgumentNullException.ThrowIfNull(exclusions);

        var draft = 0;
        var closedOrMerged = 0;
        var approved = 0;
        var untracked = 0;

        foreach (var exclusion in exclusions)
        {
            switch (exclusion)
            {
                case MembershipExclusion.Draft:
                    draft++;
                    break;
                case MembershipExclusion.ClosedOrMerged:
                    closedOrMerged++;
                    break;
                case MembershipExclusion.Approved:
                    approved++;
                    break;
                default:
                    untracked++;
                    break;
            }
        }

        return new ExclusionCounts(draft, closedOrMerged, approved, untracked);
    }
}
