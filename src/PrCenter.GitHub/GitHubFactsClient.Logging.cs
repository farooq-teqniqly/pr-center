using Microsoft.Extensions.Logging;

namespace PrCenter.GitHub;

/// <summary>
/// Source-generated log messages for <see cref="GitHubFactsClient"/>. Messages
/// carry only the owner and a controlled reason -- never the access token.
/// </summary>
internal sealed partial class GitHubFactsClient
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "GitHub review-queue fetch for owner {Owner} failed: {Reason}"
    )]
    partial void LogFetchFailed(string owner, string reason);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No GitHub token is configured for owner {Owner}"
    )]
    partial void LogTokenMissing(string owner);
}
