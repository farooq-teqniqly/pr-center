namespace PrCenter.Core.Diagnostics;

/// <summary>
/// Everything one poll recorded: the poll-level facts and one row per owner.
/// Produced once per refresh, on every exit path including the paths that
/// publish no snapshot, and fanned out to the sinks unchanged.
/// </summary>
/// <remarks>
/// Redacted by construction: identifiers, counts, instants, and
/// system-composed details only. No pull request title, body, comment text, or
/// token ever enters this record, so a sink cannot leak what it was never
/// given.
/// </remarks>
public sealed record PollDiagnostics
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PollDiagnostics"/> class.
    /// </summary>
    /// <param name="run">The poll-level facts.</param>
    /// <param name="owners">One row per owner, in the order the refresh was configured to cover them.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="run"/> or <paramref name="owners"/> is null.
    /// </exception>
    public PollDiagnostics(PollRunDiagnostics run, IReadOnlyList<OwnerPollDiagnostics> owners)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(owners);

        Run = run;
        Owners = Array.AsReadOnly(owners.ToArray());
    }

    /// <summary>Gets the poll-level facts.</summary>
    public PollRunDiagnostics Run { get; }

    /// <summary>
    /// Gets one row per owner, in the order the refresh was configured to cover
    /// them. Empty only when the owner enumeration produced nothing -- either it
    /// failed, or no owners are configured, which
    /// <see cref="PollRunDiagnostics.ConfiguredOwners"/> distinguishes.
    /// </summary>
    public IReadOnlyList<OwnerPollDiagnostics> Owners { get; }
}
