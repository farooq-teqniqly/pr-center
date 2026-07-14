using Microsoft.Extensions.Logging;

namespace PrCenter.Core.Queue;

public sealed partial class RefreshQueue
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Queue refresh aborted: the vault locked mid-poll. The previously published snapshot is left untouched."
    )]
    private partial void LogVaultLockedDuringRefresh(Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Queue refresh degraded owner '{Owner}': the fetch failed. Other owners still published."
    )]
    private partial void LogOwnerFetchFailed(string owner, Exception exception);
}
