using Microsoft.Extensions.Logging;

namespace PrCenter.Persistence;

internal sealed partial class AppSettingsStore
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Stored poll interval {StoredSeconds}s is outside the allowed range; using {ClampedSeconds}s instead. Set a valid interval on the settings screen."
    )]
    private partial void LogIntervalClamped(long storedSeconds, long clampedSeconds);
}
