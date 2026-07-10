namespace PrCenter.Core.Facts;

/// <summary>
/// A commit that landed on a pull request's branch, used to detect updates.
/// The timestamp is the instant the commit landed (committer/push date), not the
/// author date, so a rebased or cherry-picked commit is dated when it appeared.
/// Immutable data carrier with no derivation behavior.
/// </summary>
public sealed record CommitFact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommitFact"/> class.
    /// </summary>
    /// <param name="authorLogin">The login of the commit author.</param>
    /// <param name="landedAt">The instant the commit landed on the branch.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="authorLogin"/> is null, empty, or whitespace.
    /// </exception>
    public CommitFact(string authorLogin, DateTimeOffset landedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorLogin);

        AuthorLogin = authorLogin;
        LandedAt = landedAt;
    }

    /// <summary>Gets the login of the commit author.</summary>
    public string AuthorLogin { get; }

    /// <summary>Gets the instant the commit landed on the branch.</summary>
    public DateTimeOffset LandedAt { get; }
}
