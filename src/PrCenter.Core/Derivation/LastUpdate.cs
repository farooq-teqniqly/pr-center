namespace PrCenter.Core.Derivation;

/// <summary>
/// The last-updated display stamp on a queue item: who last touched the pull
/// request and when. Immutable data carrier with no derivation behavior.
/// </summary>
public sealed record LastUpdate
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LastUpdate"/> class.
    /// </summary>
    /// <param name="by">The login of whoever last updated the pull request.</param>
    /// <param name="at">The instant the pull request was last updated.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="by"/> is null, empty, or whitespace.
    /// </exception>
    public LastUpdate(string by, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(by);

        By = by;
        At = at;
    }

    /// <summary>Gets the login of whoever last updated the pull request.</summary>
    public string By { get; }

    /// <summary>Gets the instant the pull request was last updated.</summary>
    public DateTimeOffset At { get; }
}
