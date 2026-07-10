namespace PrCenter.Core.Facts;

/// <summary>
/// The transport-neutral snapshot of a pull request's reviewable facts, and the
/// intended return shape of the <c>IGitHubFacts</c> port: the GitHub adapter will
/// map API responses onto it and the queue derivers consume it. It carries exactly
/// what the membership, update, and already-covered rules read, and nothing
/// GitHub-, EF-, or ASP.NET-specific, grouped into its <see cref="Identity"/>,
/// <see cref="Status"/>, and <see cref="Activity"/>. Immutable data carrier with
/// no derivation behavior.
/// </summary>
public sealed record PullRequestFacts
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PullRequestFacts"/> class.
    /// </summary>
    /// <param name="identity">Where the pull request lives and how to display and link to it.</param>
    /// <param name="status">The pull request's lifecycle condition and last-touch stamp.</param>
    /// <param name="activity">The reviewable activity the derivers read.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="identity"/>, <paramref name="status"/>, or
    /// <paramref name="activity"/> is null.
    /// </exception>
    public PullRequestFacts(
        PullRequestIdentity identity,
        PullRequestStatus status,
        PullRequestActivity activity
    )
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(activity);

        Identity = identity;
        Status = status;
        Activity = activity;
    }

    /// <summary>Gets where the pull request lives and how to display and link to it.</summary>
    public PullRequestIdentity Identity { get; }

    /// <summary>Gets the pull request's lifecycle condition and last-touch stamp.</summary>
    public PullRequestStatus Status { get; }

    /// <summary>Gets the reviewable activity the derivers read.</summary>
    public PullRequestActivity Activity { get; }
}
