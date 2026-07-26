using Microsoft.Extensions.Logging;

namespace PrCenter.Web.Components.Locking;

/// <summary>
/// Logging declarations for <see cref="SetupCard"/>.
/// </summary>
public partial class SetupCard
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "First-run setup failed; the app password could not be stored."
    )]
    private static partial void LogSetupFailed(ILogger logger, Exception exception);
}
