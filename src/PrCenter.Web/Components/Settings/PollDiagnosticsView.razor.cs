using Microsoft.Extensions.Logging;

namespace PrCenter.Web.Components.Settings;

/// <summary>
/// Logging declarations for <see cref="PollDiagnosticsView"/>, kept out of the
/// component file so it stays markup plus display logic.
/// </summary>
public sealed partial class PollDiagnosticsView
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "The recorded poll diagnostics could not be read; the settings screen rendered the failure instead."
    )]
    private static partial void LogDiagnosticsReadFailed(ILogger logger, Exception exception);
}
