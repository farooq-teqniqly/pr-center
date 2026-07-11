namespace PrCenter.Core.Facts;

/// <summary>
/// A comment or reply on a pull request, used to detect updates. Immutable data
/// carrier with no derivation behavior.
/// </summary>
public sealed record CommentFact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommentFact"/> class.
    /// </summary>
    /// <param name="authorLogin">The login of the comment author.</param>
    /// <param name="createdAt">The instant the comment was created.</param>
    /// <param name="isBot">Whether the author is a bot or app rather than a human.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="authorLogin"/> is null, empty, or whitespace.
    /// </exception>
    public CommentFact(string authorLogin, DateTimeOffset createdAt, bool isBot = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorLogin);

        AuthorLogin = authorLogin;
        CreatedAt = createdAt;
        IsBot = isBot;
    }

    /// <summary>Gets the login of the comment author.</summary>
    public string AuthorLogin { get; }

    /// <summary>Gets the instant the comment was created.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets a value indicating whether the author is a bot or app rather than a
    /// human. Set from the GitHub actor type, never from login text.
    /// </summary>
    public bool IsBot { get; }
}
