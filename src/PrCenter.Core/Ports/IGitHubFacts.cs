namespace PrCenter.Core.Ports;

using PrCenter.Core.Facts;

/// <summary>
/// Port for reading pull-request facts from GitHub for a single owner. The
/// adapter is transport-neutral to the rest of the app; the caller supplies the
/// user's login so evaluation stays "relative to the user." Per-owner fetch
/// failures are reported through the result, never thrown. A locked token vault
/// is the one exception: it is a global precondition (the app is not unlocked),
/// not a per-owner fetch failure, so a <see cref="PrCenter.Core.Locking.VaultLockedException"/>
/// propagates from any member; the caller must ensure the vault is unlocked
/// before polling.
/// </summary>
public interface IGitHubFacts
{
    /// <summary>
    /// Gets the login of the GitHub user the owner's personal access token
    /// authenticates as, used to scope all "relative to the user" evaluation.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) whose token to use.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The authenticated user's login.</returns>
    /// <exception cref="PrCenter.Core.Locking.VaultLockedException">The token vault is locked.</exception>
    Task<string> GetAuthenticatedUserLoginAsync(
        string owner,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Fetches the facts for every open pull request in the owner relevant to the
    /// user -- those they are requested to review and those they have already
    /// reviewed -- together with the per-owner fetch status. A fetch failure is
    /// reported through the status, not thrown, so one owner cannot abort a poll
    /// covering others (a locked vault is the exception -- see the type remarks).
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) to poll.</param>
    /// <param name="myLogin">The login of the user the queue is evaluated for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The owner's fetch status and pull request facts.</returns>
    /// <exception cref="PrCenter.Core.Locking.VaultLockedException">The token vault is locked.</exception>
    Task<OwnerFactsResult> GetReviewQueueFactsAsync(
        string owner,
        string myLogin,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Fetches fresh facts for a single pull request. A closed or merged pull
    /// request still returns facts (with the closed-or-merged indicator set); the
    /// result is null only when the pull request is inaccessible or does not
    /// exist.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the pull request belongs to.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request number within the repository.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The pull request's facts, or null when it is inaccessible or gone.</returns>
    /// <exception cref="PrCenter.Core.Locking.VaultLockedException">The token vault is locked.</exception>
    Task<PullRequestFacts?> GetPullRequestFactsAsync(
        string owner,
        string repository,
        int number,
        CancellationToken cancellationToken = default
    );
}
