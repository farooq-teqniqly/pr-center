using Microsoft.Extensions.Logging;

namespace PrCenter.Core.Queue;

public sealed partial class QueueSnapshotHolder
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "A QueueSnapshotHolder.Changed subscriber faulted and was skipped; the snapshot was still published."
    )]
    private partial void LogChangedSubscriberFaulted(Exception exception);
}
