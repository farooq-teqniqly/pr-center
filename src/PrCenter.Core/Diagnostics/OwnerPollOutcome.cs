namespace PrCenter.Core.Diagnostics;

using PrCenter.Core.Ports;

/// <summary>
/// How one owner's fetch turned out within a poll: the fetch status, the
/// system-composed detail behind it, and the login the owner's token resolved
/// to. Immutable data carrier with no behavior.
/// </summary>
public sealed record OwnerPollOutcome
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerPollOutcome"/> class.
    /// </summary>
    /// <param name="status">The outcome of this owner's fetch.</param>
    /// <param name="detail">
    /// The system-composed detail behind a non-Ok status, or null. Never raw
    /// GitHub payload: the diagnostics record carries identifiers, counts, and
    /// instants only.
    /// </param>
    /// <param name="resolvedLogin">
    /// The login this owner's token resolved to, or null when resolution never
    /// completed. The token itself is never carried.
    /// </param>
    public OwnerPollOutcome(
        OwnerFetchStatus status,
        string? detail = null,
        string? resolvedLogin = null
    )
    {
        Status = status;
        Detail = detail;
        ResolvedLogin = resolvedLogin;
    }

    /// <summary>Gets the outcome of this owner's fetch.</summary>
    public OwnerFetchStatus Status { get; }

    /// <summary>Gets the system-composed detail behind a non-Ok status, or null.</summary>
    public string? Detail { get; }

    /// <summary>Gets the login this owner's token resolved to, or null when resolution never completed.</summary>
    public string? ResolvedLogin { get; }
}
