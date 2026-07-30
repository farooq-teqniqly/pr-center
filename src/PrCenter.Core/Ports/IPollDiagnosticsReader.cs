namespace PrCenter.Core.Ports;

using PrCenter.Core.Diagnostics;

/// <summary>
/// Reads back the most recently recorded polls, for the diagnostics view. Split
/// from <see cref="IPollDiagnosticsSink"/> so that nothing on the refresh or
/// derivation path can reach a read member at all.
/// </summary>
public interface IPollDiagnosticsReader
{
    /// <summary>
    /// Gets the most recently recorded polls, newest first, each with its owner
    /// rows.
    /// </summary>
    /// <param name="count">The maximum number of polls to return.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>
    /// Up to <paramref name="count"/> whole poll records, newest first; empty
    /// when nothing has been recorded.
    /// </returns>
    Task<IReadOnlyList<PollDiagnostics>> GetRecentPollsAsync(
        int count,
        CancellationToken cancellationToken = default
    );
}
