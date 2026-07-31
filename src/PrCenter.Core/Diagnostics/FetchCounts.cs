namespace PrCenter.Core.Diagnostics;

/// <summary>
/// What one owner's fetch counted. The four fetch numbers are null together
/// when nothing was fetched, because a search that never ran is a different
/// claim from one that came back empty; <see cref="CarriedOver"/> is always a
/// number, and for a failed owner it is the only count with meaning -- "5 rows
/// carried from the last good poll" and "0 rows carried, this owner has never
/// been fresh" are different situations.
/// </summary>
/// <param name="Requested">
/// The nodes the review-requested search returned before deduplication, or null
/// when nothing was fetched.
/// </param>
/// <param name="Reviewed">
/// The nodes the reviewed-by search returned before deduplication, or null when
/// nothing was fetched.
/// </param>
/// <param name="Union">
/// The distinct pull requests the two searches unioned to, or null when nothing
/// was fetched. Lower than the two counts summed whenever a pull request matched
/// both searches.
/// </param>
/// <param name="Derived">
/// The pull requests that survived derivation into shown queue items, or null
/// when nothing was fetched. <see cref="Union"/> minus every
/// <see cref="ExclusionCounts"/> tally.
/// </param>
/// <param name="CarriedOver">
/// The rows this owner carried forward from the previous snapshot because its
/// fetch failed; zero when the fetch succeeded.
/// </param>
public sealed record FetchCounts(
    int? Requested,
    int? Reviewed,
    int? Union,
    int? Derived,
    int CarriedOver
)
{
    /// <summary>
    /// Counts for an owner that was fetched successfully, carrying nothing over.
    /// </summary>
    /// <param name="requested">
    /// The review-requested search's node count, or null when the adapter
    /// reported no per-search counts for an otherwise successful fetch.
    /// </param>
    /// <param name="reviewed">The reviewed-by search's node count, or null as above.</param>
    /// <param name="union">The distinct pull requests the searches unioned to.</param>
    /// <param name="derived">The pull requests that survived derivation.</param>
    /// <returns>The fetch counts.</returns>
    public static FetchCounts Fetched(int? requested, int? reviewed, int union, int derived) =>
        new(requested, reviewed, union, derived, 0);

    /// <summary>
    /// Counts for an owner whose fetch failed, so its rows came from the previous
    /// snapshot and every fetch number reads as absent rather than as zero.
    /// </summary>
    /// <param name="carriedOver">The rows carried forward from the previous snapshot.</param>
    /// <returns>The fetch counts.</returns>
    public static FetchCounts NothingFetched(int carriedOver) =>
        new(null, null, null, null, carriedOver);
}
