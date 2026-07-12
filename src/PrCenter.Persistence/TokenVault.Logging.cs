using Microsoft.Extensions.Logging;

namespace PrCenter.Persistence;

internal sealed partial class TokenVault
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Vault reset: all owner tokens and the app-security row were wiped. Tokens must be re-entered; there is no recovery."
    )]
    private partial void LogVaultReset();
}
