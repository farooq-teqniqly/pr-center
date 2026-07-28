namespace PrCenter.Web.Polling;

internal sealed partial class QueuePollingService
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "A poll cycle failed. Polling continues on the next interval."
    )]
    private partial void LogPollCycleFailed(Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Reading the stored poll interval failed; falling back to {Fallback} for this cycle."
    )]
    private partial void LogIntervalReadFailed(Exception exception, TimeSpan fallback);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Reading the app-lock state failed, so this wake polled nothing. Polling continues on the next interval."
    )]
    private partial void LogLockStateReadFailed(Exception exception);
}
