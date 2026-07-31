namespace PrCenter.Core.Diagnostics;

using System.Globalization;
using PrCenter.Core.Facts;

/// <summary>
/// The pull requests one owner's fetch contributed to a poll, as
/// <c>owner/repo#number</c> identifiers, together with how many of them belong
/// to a different owner. The two travel as one concept because the count is
/// meaningless apart from the list it counts, and computing one without the
/// other is a bug.
/// </summary>
/// <remarks>
/// A non-zero foreign count is normal, not a fault: owner-queue discovery is not
/// scoped to the owner, so a token whose resource owner is one org can still see
/// repositories in another configured owner. The value is attribution -- it turns
/// "two owners saw the same pull request" into "this token is the one reaching
/// across". Titles and bodies are never carried; the identifier is the whole
/// content.
/// </remarks>
public sealed record ContributedPullRequests
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContributedPullRequests"/> class.
    /// </summary>
    /// <param name="ids">The contributed <c>owner/repo#number</c> identifiers.</param>
    /// <param name="foreignCount">How many identifiers belong to a different owner.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ids"/> is null.</exception>
    public ContributedPullRequests(IReadOnlyList<string> ids, int foreignCount)
    {
        ArgumentNullException.ThrowIfNull(ids);

        Ids = Array.AsReadOnly(ids.ToArray());
        ForeignCount = foreignCount;
    }

    /// <summary>Gets the contributed <c>owner/repo#number</c> identifiers.</summary>
    public IReadOnlyList<string> Ids { get; }

    /// <summary>Gets how many of the identifiers belong to an owner other than the row's own.</summary>
    public int ForeignCount { get; }

    /// <summary>Gets an empty contribution, for an owner that produced nothing.</summary>
    public static ContributedPullRequests None { get; } = new([], 0);

    /// <summary>
    /// Formats the identities one owner contributed and counts those belonging to
    /// a different owner, in one pass, so the list and its foreign count can
    /// never disagree.
    /// </summary>
    /// <param name="owner">The owner whose row this is.</param>
    /// <param name="identities">The pull request identities the owner contributed.</param>
    /// <returns>The contributed identifiers and their foreign count.</returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="identities"/> is null.</exception>
    public static ContributedPullRequests For(
        string owner,
        IEnumerable<PullRequestIdentity> identities
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(identities);

        var ids = new List<string>();
        var foreignCount = 0;

        foreach (var identity in identities)
        {
            ids.Add(Format(identity));

            // Owner logins are identifiers, and GitHub treats them
            // case-insensitively, so a case difference is the same owner.
            if (!string.Equals(identity.Owner, owner, StringComparison.OrdinalIgnoreCase))
            {
                foreignCount++;
            }
        }

        return new ContributedPullRequests(ids, foreignCount);
    }

    private static string Format(PullRequestIdentity identity) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{identity.Owner}/{identity.Repository}#{identity.Number}"
        );
}
