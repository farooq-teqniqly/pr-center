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

    /// <summary>
    /// The refresh never reached this owner -- it aborted or was canceled first.
    /// Not a fetch failure: nothing was attempted, so the owner's counts read as
    /// absent rather than as zero, and a reader must not present it as broken.
    /// </summary>
    /// <remarks>
    /// Diagnostics rows only. A published <c>QueueSnapshot</c> never carries this
    /// status, because an aborted refresh does not publish at all -- so the
    /// queue's own status indicators cannot encounter it.
    /// </remarks>
    NotPolled,
}
