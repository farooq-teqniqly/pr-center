using Microsoft.Extensions.Logging;

namespace PrCenter.Core.Queue;

public sealed partial class RefreshStateHolder
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "A RefreshStateHolder.Changed subscriber faulted and was skipped; the refresh state was still published."
    )]
    private partial void LogChangedSubscriberFaulted(Exception exception);
}
