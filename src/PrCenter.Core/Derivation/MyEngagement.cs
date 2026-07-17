namespace PrCenter.Core.Derivation;

/// <summary>
/// The user's own engagement with a pull request: when they last looked at it
/// and when they last reviewed it. Either is <see langword="null"/> for "never",
/// a real state the UI renders. Immutable data carrier with no derivation
/// behavior.
/// </summary>
public sealed record MyEngagement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MyEngagement"/> class.
    /// </summary>
    /// <param name="lastLookedAt">
    /// The instant the user last looked at the pull request, or
    /// <see langword="null"/> if they never have.
    /// </param>
    /// <param name="lastReviewedAt">
    /// The instant the user last submitted a review, regardless of its state, or
    /// <see langword="null"/> if they never have.
    /// </param>
    public MyEngagement(DateTimeOffset? lastLookedAt, DateTimeOffset? lastReviewedAt)
    {
        LastLookedAt = lastLookedAt;
        LastReviewedAt = lastReviewedAt;
    }

    /// <summary>
    /// Gets the instant the user last looked at the pull request, or
    /// <see langword="null"/> if they never have.
    /// </summary>
    public DateTimeOffset? LastLookedAt { get; }

    /// <summary>
    /// Gets the instant the user last submitted a review, regardless of its
    /// state, or <see langword="null"/> if they never have.
    /// </summary>
    public DateTimeOffset? LastReviewedAt { get; }
}
