namespace PrCenter.Core.Queue;

/// <summary>
/// How one review-queue refresh ended, as seen by the poll loop that ran it. The
/// closed set of cases is <see cref="RefreshSucceeded"/> and
/// <see cref="RefreshAbortedByLock"/>. A single owner's fetch failure is not a case
/// here: it degrades that owner's <see cref="OwnerStatus"/> and the refresh still
/// succeeds, because a snapshot was published.
/// </summary>
public abstract record RefreshOutcome
{
    // Private protected so the case set stays closed to this assembly: an outcome
    // the poll loop cannot name is one it cannot report to the user.
    private protected RefreshOutcome() { }
}
