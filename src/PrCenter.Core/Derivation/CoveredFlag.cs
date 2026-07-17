namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Derives the "already covered" decoration: the other human reviewers who have
/// weighed in, signaling the user's review has lower marginal value. Bot/CI
/// reviews are not human coverage and never count. Pure function of current
/// facts; never hides or moves the pull request.
/// </summary>
internal static class CoveredFlag
{
    /// <summary>
    /// Determines the distinct other-human reviewers who already cover a pull request.
    /// </summary>
    /// <param name="facts">The pull request's current facts.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <returns>
    /// The distinct logins of other humans who submitted a review, in no
    /// guaranteed order; empty when only pending requests, the user's own
    /// reviews, or bot reviews exist. The pull request is covered when the list
    /// is non-empty.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="myLogin"/> is null, empty, or whitespace.
    /// </exception>
    public static IReadOnlyList<string> CoveringLogins(PullRequestFacts facts, string myLogin)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        return facts
            .Activity.Reviews.Where(review =>
                !review.IsBot && GitHubLogin.NotMe(review.ReviewerLogin, myLogin)
            )
            .Select(review => review.ReviewerLogin)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            .AsReadOnly();
    }
}
