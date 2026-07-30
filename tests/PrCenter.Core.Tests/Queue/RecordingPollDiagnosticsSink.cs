using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Core.Tests.Queue;

/// <summary>
/// A sink that keeps every record written to it, and the token each write was
/// made with, so tests can assert both what was recorded and that the write did
/// not ride the caller's token. Optionally throws, to exercise sink isolation.
/// </summary>
internal sealed class RecordingPollDiagnosticsSink : IPollDiagnosticsSink
{
    private readonly Exception? _throws;
    private readonly List<PollDiagnostics> _records = [];

    public RecordingPollDiagnosticsSink(Exception? throws = null) => _throws = throws;

    public IReadOnlyList<PollDiagnostics> Records => _records;

    public PollDiagnostics? Single => _records.Count == 1 ? _records[0] : null;

    public bool WriteWasCanceled { get; private set; }

    public Task WriteAsync(
        PollDiagnostics diagnostics,
        CancellationToken cancellationToken = default
    )
    {
        WriteWasCanceled = cancellationToken.IsCancellationRequested;

        if (_throws is not null)
        {
            return Task.FromException(_throws);
        }

        _records.Add(diagnostics);
        return Task.CompletedTask;
    }
}
