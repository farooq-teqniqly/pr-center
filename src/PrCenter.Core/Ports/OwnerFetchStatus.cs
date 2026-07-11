namespace PrCenter.Core.Ports;

/// <summary>
/// The outcome of fetching one owner's review queue, so a single broken owner
/// surfaces as a status rather than aborting a poll over the other owners.
/// </summary>
public enum OwnerFetchStatus
{
    /// <summary>The fetch succeeded; the facts list is authoritative (possibly empty).</summary>
    Ok,

    /// <summary>
    /// The owner's token was rejected (authentication or authorization failure),
    /// for example a personal access token created with the wrong resource owner.
    /// </summary>
    MisconfiguredToken,

    /// <summary>
    /// The fetch failed for a transient or unexpected reason -- rate-limit
    /// exhaustion, a network failure, a server error, or a malformed payload.
    /// </summary>
    Error,
}
