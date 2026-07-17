namespace PrCenter.Core.Derivation;

/// <summary>
/// One reviewer on a pull request's roster: the union of the directly requested
/// reviewers and the reviewers who have submitted a standing review. Immutable
/// data carrier with no derivation behavior; ordering and styling (for example
/// bot chips or the dashed "me" ring) are a presentation concern.
/// </summary>
public sealed record ReviewerRosterEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewerRosterEntry"/> class.
    /// </summary>
    /// <param name="login">The reviewer's login.</param>
    /// <param name="state">The reviewer's standing in the roster.</param>
    /// <param name="isBot">Whether the reviewer is a bot or app rather than a human.</param>
    /// <param name="isMe">Whether the reviewer is the user the queue is evaluated for.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="login"/> is null, empty, or whitespace.
    /// </exception>
    public ReviewerRosterEntry(string login, ReviewerState state, bool isBot, bool isMe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);

        Login = login;
        State = state;
        IsBot = isBot;
        IsMe = isMe;
    }

    /// <summary>Gets the reviewer's login.</summary>
    public string Login { get; }

    /// <summary>Gets the reviewer's standing in the roster.</summary>
    public ReviewerState State { get; }

    /// <summary>
    /// Gets a value indicating whether the reviewer is a bot or app rather than a
    /// human. A requested-but-not-yet-reviewed reviewer is always false: a pending
    /// request arrives as a plain login with no actor type to read.
    /// </summary>
    public bool IsBot { get; }

    /// <summary>Gets a value indicating whether the reviewer is the user the queue is evaluated for.</summary>
    public bool IsMe { get; }
}
