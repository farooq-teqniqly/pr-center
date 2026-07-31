using Microsoft.Extensions.Time.Testing;
using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Core.Tests.Queue;

/// <summary>
/// A sink whose write spends wall-clock time before completing, so a test can
/// assert that one slow sink does not consume the write budget the sinks after
/// it were promised.
/// </summary>
internal sealed class BudgetSpendingPollDiagnosticsSink : IPollDiagnosticsSink
{
    private readonly FakeTimeProvider _clock;
    private readonly TimeSpan _spends;

    public BudgetSpendingPollDiagnosticsSink(FakeTimeProvider clock, TimeSpan spends)
    {
        _clock = clock;
        _spends = spends;
    }

    public Task WriteAsync(
        PollDiagnostics diagnostics,
        CancellationToken cancellationToken = default
    )
    {
        _clock.Advance(_spends);
        return Task.CompletedTask;
    }
}
