namespace PrCenter.Core.Derivation;

using PrCenter.Core.Facts;

/// <summary>
/// Derives the "already covered" decoration: whether another human reviewer has
/// weighed in, signaling the user's review has lower marginal value. Bot/CI
/// reviews are not human coverage and never count. Pure function of current
/// facts; never hides or moves the pull request.
/// </summary>
internal static class CoveredFlag
{
    /// <summary>
    /// Determines whether a pull request is already covered by another reviewer.
    /// </summary>
    /// <param name="facts">The pull request's current facts.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <returns>
    /// <see langword="true"/> when at least one submitted review is by another
    /// human; pending review requests and bot reviews do not count.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="myLogin"/> is null, empty, or whitespace.
    /// </exception>
    public static bool IsCovered(PullRequestFacts facts, string myLogin)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentException.ThrowIfNullOrWhiteSpace(myLogin);

        return facts.Activity.Reviews.Any(review =>
            !review.IsBot && GitHubLogin.NotMe(review.ReviewerLogin, myLogin)
        );
    }
}
