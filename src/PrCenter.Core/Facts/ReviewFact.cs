namespace PrCenter.Core.Facts;

/// <summary>
/// A single submitted review on a pull request, evaluated later relative to the
/// user. Immutable data carrier with no derivation behavior.
/// </summary>
public sealed record ReviewFact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewFact"/> class.
    /// </summary>
    /// <param name="reviewerLogin">The login of the reviewer who submitted the review.</param>
    /// <param name="state">The verdict the reviewer submitted.</param>
    /// <param name="submittedAt">The instant the review was submitted.</param>
    /// <param name="isBot">Whether the reviewer is a bot or app rather than a human.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="reviewerLogin"/> is null, empty, or whitespace.
    /// </exception>
    public ReviewFact(
        string reviewerLogin,
        ReviewState state,
        DateTimeOffset submittedAt,
        bool isBot = false
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewerLogin);

        ReviewerLogin = reviewerLogin;
        State = state;
        SubmittedAt = submittedAt;
        IsBot = isBot;
    }

    /// <summary>Gets the login of the reviewer who submitted the review.</summary>
    public string ReviewerLogin { get; }

    /// <summary>Gets the verdict the reviewer submitted.</summary>
    public ReviewState State { get; }

    /// <summary>Gets the instant the review was submitted.</summary>
    public DateTimeOffset SubmittedAt { get; }

    /// <summary>
    /// Gets a value indicating whether the reviewer is a bot or app rather than a
    /// human. Set from the GitHub actor type, never from login text.
    /// </summary>
    public bool IsBot { get; }
}
