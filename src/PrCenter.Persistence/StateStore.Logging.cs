using Microsoft.Extensions.Logging;

namespace PrCenter.Persistence;

internal sealed partial class StateStore
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Lost the insert race for last-seen marker {PullRequestId}; recovered by updating the existing row."
    )]
    private partial void LogRecoveredFromInsertRace(string pullRequestId);
}
