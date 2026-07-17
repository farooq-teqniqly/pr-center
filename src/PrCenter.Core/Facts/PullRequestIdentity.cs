namespace PrCenter.Core.Facts;

/// <summary>
/// Where a pull request lives and how to display and link to it. The
/// <see cref="Id"/> is the stable key used for the last-seen marker. Immutable
/// data carrier with no derivation behavior.
/// </summary>
public sealed record PullRequestIdentity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestIdentity"/> class.
    /// </summary>
    /// <param name="id">The stable identifier used as the last-seen marker key.</param>
    /// <param name="owner">The GitHub owner (org or account) the pull request belongs to.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request number within the repository.</param>
    /// <param name="title">The pull request title.</param>
    /// <param name="url">The pull request's web URL.</param>
    /// <param name="authorLogin">The login of whoever opened the pull request, for display.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/>, <paramref name="owner"/>,
    /// <paramref name="repository"/>, <paramref name="title"/>,
    /// <paramref name="url"/>, or <paramref name="authorLogin"/> is null, empty,
    /// or whitespace.
    /// </exception>
    public PullRequestIdentity(
        string id,
        string owner,
        string repository,
        int number,
        string title,
        string url,
        string authorLogin
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorLogin);

        Id = id;
        Owner = owner;
        Repository = repository;
        Number = number;
        Title = title;
        Url = url;
        AuthorLogin = authorLogin;
    }

    /// <summary>Gets the stable identifier used as the last-seen marker key.</summary>
    public string Id { get; }

    /// <summary>Gets the GitHub owner (org or account) the pull request belongs to.</summary>
    public string Owner { get; }

    /// <summary>Gets the repository name.</summary>
    public string Repository { get; }

    /// <summary>Gets the pull request number within the repository.</summary>
    public int Number { get; }

    /// <summary>Gets the pull request title.</summary>
    public string Title { get; }

    /// <summary>Gets the pull request's web URL.</summary>
    public string Url { get; }

    /// <summary>Gets the login of whoever opened the pull request, for display.</summary>
    public string AuthorLogin { get; }
}
