namespace PrCenter.Core.Queue;

/// <summary>
/// An immutable view of the poll loop's refresh activity: whether a refresh is
/// running right now, when the last attempt was made, and how that attempt failed.
/// Published by the poll loop and read by the inbox so a manual refresh can be
/// held off while one is already in flight. A null <see cref="LastAttemptAt"/> is
/// the never-refreshed state; a null <see cref="Failure"/> means the last attempt
/// succeeded. <see cref="Failure"/> carries only a whole-refresh failure -- a
/// single owner's fetch failure degrades that owner's <see cref="OwnerStatus"/>
/// instead, and the refresh still counts as succeeded.
/// </summary>
/// <param name="InProgress">Whether a refresh is running right now.</param>
/// <param name="LastAttemptAt">
/// The instant the last refresh attempt started, or <see langword="null"/> when no
/// refresh has been attempted since process start.
/// </param>
/// <param name="Failure">
/// A description of how the last attempt failed, or <see langword="null"/> when it
/// succeeded or none has been made.
/// </param>
public sealed record RefreshState(bool InProgress, DateTimeOffset? LastAttemptAt, string? Failure)
{
    /// <summary>
    /// Gets the state before any refresh has been attempted: idle, never attempted,
    /// with no failure to report.
    /// </summary>
    public static RefreshState NeverRefreshed { get; } =
        new(InProgress: false, LastAttemptAt: null, Failure: null);
}
