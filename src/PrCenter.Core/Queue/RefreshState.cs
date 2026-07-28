namespace PrCenter.Core.Queue;

/// <summary>
/// An immutable view of the poll loop's refresh activity: whether a refresh is
/// running right now, when the last one finished, and how it failed. Published by
/// the poll loop and read by the inbox so a manual refresh can be held off while
/// one is already in flight. A null <see cref="LastCompletedAt"/> is the
/// never-refreshed state; a null <see cref="Failure"/> means the last refresh
/// succeeded. <see cref="Failure"/> carries only a whole-refresh failure -- a
/// single owner's fetch failure degrades that owner's <see cref="OwnerStatus"/>
/// instead, and the refresh still counts as succeeded.
/// </summary>
/// <param name="InProgress">
/// Whether the loop is servicing a refresh request right now. Set from the moment
/// a wake takes the request up -- before it knows whether the app is unlocked
/// enough to poll -- and cleared when that wake ends, whether it polled or skipped.
/// </param>
/// <param name="LastCompletedAt">
/// The instant the last refresh finished, or <see langword="null"/> when none has
/// finished since process start. This is a completion instant, not a start one:
/// a refresh still running has not refreshed anything yet, so the inbox keeps
/// showing the previous completion until this one lands.
/// </param>
/// <param name="Failure">
/// A description of how the last refresh failed, or <see langword="null"/> when it
/// succeeded or none has run.
/// </param>
public sealed record RefreshState(bool InProgress, DateTimeOffset? LastCompletedAt, string? Failure)
{
    /// <summary>
    /// Gets the state before any refresh has run: idle, never completed, with no
    /// failure to report.
    /// </summary>
    public static RefreshState NeverRefreshed { get; } =
        new(InProgress: false, LastCompletedAt: null, Failure: null);
}
