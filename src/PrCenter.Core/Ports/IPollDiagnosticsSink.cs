namespace PrCenter.Core.Ports;

using PrCenter.Core.Diagnostics;

/// <summary>
/// A destination one poll's diagnostics record is written to. Implementations
/// fan out from a single producer: local storage, and later a telemetry
/// exporter.
/// </summary>
/// <remarks>
/// The absence of a read member is deliberate, and it is the enforcement of the
/// invariant that no derivation path reads diagnostics. Membership, update, and
/// covered decisions are pure functions of current GitHub facts; a deriver that
/// could read what an earlier poll recorded would be a stored transition
/// machine wearing a diagnostics hat. Reading is a separate port,
/// <see cref="IPollDiagnosticsReader"/>, which the queue and derivation
/// namespaces do not reference -- asserted by an architecture test, since the
/// compiler cannot express it.
/// </remarks>
public interface IPollDiagnosticsSink
{
    /// <summary>
    /// Writes one poll's diagnostics record.
    /// </summary>
    /// <param name="diagnostics">The record to write.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>A task that completes when the record has been written.</returns>
    Task WriteAsync(PollDiagnostics diagnostics, CancellationToken cancellationToken = default);
}
