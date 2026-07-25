using Microsoft.Extensions.Logging;

namespace PrCenter.Web.Components.Locking;

/// <summary>
/// Logging declarations for <see cref="UnlockCard"/>.
/// </summary>
public partial class UnlockCard
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Unlock failed unexpectedly; the stored vault data could not be read."
    )]
    private static partial void LogUnlockFailed(ILogger logger, Exception exception);
}
