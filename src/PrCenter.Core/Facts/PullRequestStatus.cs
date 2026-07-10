namespace PrCenter.Core.Facts;

/// <summary>
/// The current lifecycle condition of a pull request plus its last-touch
/// display stamp: whether it is a draft or closed/merged (which drive
/// membership), and who last updated it and when (for display). Immutable data
/// carrier with no derivation behavior.
/// </summary>
public sealed record PullRequestStatus
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestStatus"/> class.
    /// </summary>
    /// <param name="isDraft">Whether the pull request is a draft.</param>
    /// <param name="isClosedOrMerged">Whether the pull request is closed or merged.</param>
    /// <param name="lastUpdatedBy">The login of whoever last updated the pull request, for display.</param>
    /// <param name="lastUpdatedAt">The instant the pull request was last updated, for display.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="lastUpdatedBy"/> is null, empty, or whitespace.
    /// </exception>
    public PullRequestStatus(
        bool isDraft,
        bool isClosedOrMerged,
        string lastUpdatedBy,
        DateTimeOffset lastUpdatedAt
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lastUpdatedBy);

        IsDraft = isDraft;
        IsClosedOrMerged = isClosedOrMerged;
        LastUpdatedBy = lastUpdatedBy;
        LastUpdatedAt = lastUpdatedAt;
    }

    /// <summary>Gets a value indicating whether the pull request is a draft.</summary>
    public bool IsDraft { get; }

    /// <summary>Gets a value indicating whether the pull request is closed or merged.</summary>
    public bool IsClosedOrMerged { get; }

    /// <summary>Gets the login of whoever last updated the pull request, for display.</summary>
    public string LastUpdatedBy { get; }

    /// <summary>Gets the instant the pull request was last updated, for display.</summary>
    public DateTimeOffset LastUpdatedAt { get; }
}
