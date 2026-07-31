using Microsoft.Extensions.Logging;

namespace PrCenter.Persistence;

internal sealed partial class SqlitePollDiagnosticsReader
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Dropped poll diagnostics record {PollId} from the read: a stored column could not be read back."
    )]
    private partial void LogPollDropped(Guid pollId, Exception exception);
}
