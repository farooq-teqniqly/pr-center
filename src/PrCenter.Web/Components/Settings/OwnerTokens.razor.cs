using Microsoft.Extensions.Logging;

namespace PrCenter.Web.Components.Settings;

/// <summary>
/// Logging declarations for <see cref="OwnerTokens"/>. Sealed here because the
/// component implements <see cref="IDisposable"/>: without it the analyzer wants
/// the full virtual dispose pattern, which a leaf component has no use for.
/// </summary>
public sealed partial class OwnerTokens
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "An owner token write failed because the vault was locked; the settings screen reported it to the user."
    )]
    private static partial void LogOwnerTokenWriteFailed(ILogger logger, Exception exception);
}
