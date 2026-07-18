namespace PrCenter.Core.Derivation;

/// <summary>
/// The user's own engagement with a pull request: when they last reviewed it,
/// or <see langword="null"/> for "never", a real state the UI renders. This same
/// instant is the update baseline. Immutable data carrier with no derivation
/// behavior.
/// </summary>
public sealed record MyEngagement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MyEngagement"/> class.
    /// </summary>
    /// <param name="lastReviewedAt">
    /// The instant the user last submitted a review, regardless of its state, or
    /// <see langword="null"/> if they never have.
    /// </param>
    public MyEngagement(DateTimeOffset? lastReviewedAt)
    {
        LastReviewedAt = lastReviewedAt;
    }

    /// <summary>
    /// Gets the instant the user last submitted a review, regardless of its
    /// state, or <see langword="null"/> if they never have.
    /// </summary>
    public DateTimeOffset? LastReviewedAt { get; }
}
