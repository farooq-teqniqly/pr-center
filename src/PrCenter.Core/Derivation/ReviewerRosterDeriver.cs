namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Derives a pull request's reviewer roster: the union of the directly requested
/// reviewers and the reviewers who submitted a standing review. A reviewer with
/// reviews takes their latest review's state; a requested reviewer who has not
/// reviewed is <see cref="ReviewerState.Pending"/>. Dismissed reviews never reach
/// the facts, so "latest review" already means "latest standing review". Pure
/// function of current facts; imposes no ordering.
/// </summary>
internal static class ReviewerRosterDeriver
{
    /// <summary>
    /// Derives the reviewer roster for a pull request relative to the user.
    /// </summary>
    /// <param name="facts">The pull request's current facts.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <returns>
    /// One entry per distinct reviewer, in no guaranteed order. A reviewer who is
    /// both requested and has reviewed appears once, with their review state.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="myLogin"/> is null, empty, or whitespace.
    /// </exception>
    public static IReadOnlyList<ReviewerRosterEntry> Derive(PullRequestFacts facts, string myLogin)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        var entries = new List<ReviewerRosterEntry>();
        var accountedFor = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reviewer in ReviewersByLatestReview(facts))
        {
            entries.Add(
                new ReviewerRosterEntry(
                    reviewer.ReviewerLogin,
                    ToReviewerState(reviewer.State),
                    reviewer.IsBot,
                    GitHubLogin.IsMe(reviewer.ReviewerLogin, myLogin)
                )
            );
            accountedFor.Add(reviewer.ReviewerLogin);
        }

        var pendingLogins = facts
            .Activity.RequestedReviewerLogins.Where(login => !accountedFor.Contains(login))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var login in pendingLogins)
        {
            entries.Add(
                new ReviewerRosterEntry(
                    login,
                    ReviewerState.Pending,
                    isBot: false,
                    GitHubLogin.IsMe(login, myLogin)
                )
            );
        }

        return entries;
    }

    // One review per reviewer -- the latest by submitted timestamp, so a reviewer
    // who reviewed more than once takes their standing verdict.
    private static IEnumerable<ReviewFact> ReviewersByLatestReview(PullRequestFacts facts) =>
        facts
            .Activity.Reviews.GroupBy(
                review => review.ReviewerLogin,
                StringComparer.OrdinalIgnoreCase
            )
            .Select(group => group.OrderByDescending(review => review.SubmittedAt).First());

    private static ReviewerState ToReviewerState(ReviewState state) =>
        state switch
        {
            ReviewState.Approved => ReviewerState.Approved,
            ReviewState.ChangesRequested => ReviewerState.ChangesRequested,
            ReviewState.Commented => ReviewerState.Commented,
            _ => throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Unknown review state."
            ),
        };
}
