using Microsoft.Extensions.Logging;

namespace PrCenter.Persistence;

internal sealed partial class SqlitePollDiagnosticsSink
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Evicted {Evicted} poll diagnostics record(s) to keep the ring at {Retained}."
    )]
    private partial void LogPollsEvicted(int evicted, int retained);
}
