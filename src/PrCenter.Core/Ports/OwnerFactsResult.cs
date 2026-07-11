namespace PrCenter.Core.Ports;

using PrCenter.Core.Facts;

/// <summary>
/// The result of fetching one owner's review queue: a fetch status, the pull
/// request facts (empty on failure), and an optional human-readable detail for
/// the per-owner status indicator. Immutable data carrier with no behavior.
/// </summary>
public sealed record OwnerFactsResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerFactsResult"/> class.
    /// </summary>
    /// <param name="status">The fetch outcome for the owner.</param>
    /// <param name="facts">The pull request facts (empty on failure).</param>
    /// <param name="detail">An optional human-readable detail for the status indicator.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    public OwnerFactsResult(
        OwnerFetchStatus status,
        IReadOnlyList<PullRequestFacts> facts,
        string? detail = null
    )
    {
        ArgumentNullException.ThrowIfNull(facts);

        // Copy into a read-only wrapper so the result cannot be mutated through
        // the caller's reference or by casting the property back to an array.
        Status = status;
        Facts = Array.AsReadOnly(facts.ToArray());
        Detail = detail;
    }

    /// <summary>Gets the fetch outcome for the owner.</summary>
    public OwnerFetchStatus Status { get; }

    /// <summary>Gets the pull request facts; empty when the fetch failed.</summary>
    public IReadOnlyList<PullRequestFacts> Facts { get; }

    /// <summary>Gets an optional human-readable detail for the status indicator, or null.</summary>
    public string? Detail { get; }
}
