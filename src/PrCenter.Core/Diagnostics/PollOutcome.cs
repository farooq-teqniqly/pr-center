namespace PrCenter.Core.Diagnostics;

/// <summary>
/// How one refresh left: the four ways <c>RefreshQueue.ExecuteAsync</c> can
/// return or throw. Every poll records exactly one of these, including the
/// paths that publish no snapshot.
/// </summary>
public enum PollOutcome
{
    /// <summary>Every configured owner was polled and a snapshot was published.</summary>
    Succeeded,

    /// <summary>
    /// The vault locked mid-refresh, so the refresh was abandoned without
    /// publishing and the last good snapshot survives. Owners the loop never
    /// reached carry <see cref="Ports.OwnerFetchStatus.NotPolled"/>.
    /// </summary>
    AbortedByLock,

    /// <summary>
    /// The refresh was canceled, normally by host shutdown mid-poll. Expected
    /// rather than exceptional -- a poll that never finished is worth recording,
    /// but it is not a failure and must not be presented as one.
    /// </summary>
    Canceled,

    /// <summary>
    /// The refresh threw: the owner enumeration failed, or publishing did. Not
    /// a per-owner fetch failure, which degrades only its own owner and leaves
    /// the poll succeeding.
    /// </summary>
    Faulted,
}
