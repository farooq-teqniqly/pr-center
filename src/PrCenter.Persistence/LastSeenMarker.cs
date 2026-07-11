namespace PrCenter.Persistence;

/// <summary>
/// The persisted record of when the user last looked at a pull request, keyed
/// by the pull request's stable id. One row per pull request; never deleted.
/// </summary>
internal sealed class LastSeenMarker
{
    /// <summary>Gets or sets the pull request's stable id (the primary key).</summary>
    public string PullRequestId { get; set; } = null!;

    /// <summary>Gets or sets the instant the user last looked at the pull request.</summary>
    public DateTimeOffset SeenAt { get; set; }
}
