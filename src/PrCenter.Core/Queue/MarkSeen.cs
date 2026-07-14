using PrCenter.Core.Facts;
using PrCenter.Core.Ports;

namespace PrCenter.Core.Queue;

/// <summary>
/// Use case for click-through mark-as-seen: it live-fetches fresh facts for a
/// single pull request and records the last-seen marker as the high-water mark of
/// activity that existed when the user looked -- the maximum timestamp across the
/// fetched commits, comments, and reviews, including the user's own and bots'.
/// The marker is never taken from the local wall clock, so it stays in GitHub's
/// timestamp domain and is immune to clock skew. When the live fetch returns
/// nothing (the pull request is inaccessible or gone), no marker is written.
/// </summary>
public sealed class MarkSeen
{
    private readonly IGitHubFacts _facts;
    private readonly IStateStore _stateStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkSeen"/> class.
    /// </summary>
    /// <param name="facts">The GitHub facts port for the live single-pull-request fetch.</param>
    /// <param name="stateStore">The store the last-seen marker is written to.</param>
    public MarkSeen(IGitHubFacts facts, IStateStore stateStore)
    {
        _facts = facts;
        _stateStore = stateStore;
    }

    /// <summary>
    /// Fetches fresh facts for the pull request and writes its last-seen marker,
    /// or writes nothing when the pull request is inaccessible or gone.
    /// </summary>
    /// <param name="owner">The GitHub owner (org or account) the pull request belongs to.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="number">The pull request number within the repository.</param>
    /// <param name="pullRequestId">The stable marker key for the pull request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the marker has been written or skipped.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="owner"/>, <paramref name="repository"/>, or
    /// <paramref name="pullRequestId"/> is null, empty, or whitespace.
    /// </exception>
    public async Task MarkSeenAsync(
        string owner,
        string repository,
        int number,
        string pullRequestId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(pullRequestId);

        var facts = await _facts
            .GetPullRequestFactsAsync(owner, repository, number, cancellationToken)
            .ConfigureAwait(false);
        if (facts is null)
        {
            return;
        }

        await _stateStore
            .SetLastSeenAsync(pullRequestId, HighWaterMark(facts), cancellationToken)
            .ConfigureAwait(false);
    }

    // The maximum activity timestamp across every fetched event -- own and bot
    // events included, since the marker is a high-water mark of what existed when
    // the user looked, not an update judgment. With no activity events (defensive:
    // a real pull request always has a commit) it falls back to the last-touch
    // stamp, keeping the marker in GitHub's timestamp domain.
    private static DateTimeOffset HighWaterMark(PullRequestFacts facts)
    {
        var activity = facts.Activity;
        return activity
            .Commits.Select(commit => commit.LandedAt)
            .Concat(activity.Comments.Select(comment => comment.CreatedAt))
            .Concat(activity.Reviews.Select(review => review.SubmittedAt))
            .DefaultIfEmpty(facts.Status.LastUpdatedAt)
            .Max();
    }
}
