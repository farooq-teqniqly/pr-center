using Microsoft.Extensions.Logging;

namespace PrCenter.Persistence;

internal sealed partial class AppLock
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Unlock attempt failed: the app password did not verify against the stored sentinel."
    )]
    private partial void LogUnlockFailed();
}
