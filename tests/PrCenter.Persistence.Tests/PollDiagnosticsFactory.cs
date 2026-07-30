using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Persistence.Tests;

/// <summary>
/// Builds <see cref="PollDiagnostics"/> records for the sink and reader tests.
/// </summary>
internal static class PollDiagnosticsFactory
{
    public static readonly DateTimeOffset StartedAt = new(2026, 7, 29, 14, 5, 0, TimeSpan.Zero);

    /// <summary>A fully populated record: every count, the rate limit, and two owner rows.</summary>
    public static PollDiagnostics Full(Guid? pollId = null) =>
        new(
            new PollRunDiagnostics(
                pollId ?? Guid.NewGuid(),
                StartedAt,
                StartedAt.AddSeconds(3),
                PollOutcome.Succeeded,
                ["acme", "ps-unite"],
                publishedCount: 5
            ),
            [PolledRow("acme"), CarriedOverRow("ps-unite")]
        );

    /// <summary>A record with no owner rows, as an enumeration fault produces.</summary>
    public static PollDiagnostics WithoutOwners(
        Guid? pollId = null,
        PollOutcome outcome = PollOutcome.Faulted
    ) =>
        new(
            new PollRunDiagnostics(
                pollId ?? Guid.NewGuid(),
                StartedAt,
                StartedAt.AddSeconds(1),
                outcome
            ),
            []
        );

    /// <summary>A record whose single owner row was never reached.</summary>
    public static PollDiagnostics WithUnreachedOwner(Guid? pollId = null) =>
        new(
            new PollRunDiagnostics(
                pollId ?? Guid.NewGuid(),
                StartedAt,
                StartedAt.AddSeconds(1),
                PollOutcome.AbortedByLock,
                ["acme"]
            ),
            [UnreachedRow("acme")]
        );

    /// <summary>A record carrying the given number of owner rows, each polled.</summary>
    public static PollDiagnostics WithOwnerCount(int owners)
    {
        var names = Enumerable.Range(1, owners).Select(index => $"owner-{index}").ToArray();
        return new PollDiagnostics(
            new PollRunDiagnostics(
                Guid.NewGuid(),
                StartedAt,
                StartedAt.AddSeconds(2),
                PollOutcome.Succeeded,
                names,
                publishedCount: owners
            ),
            [.. names.Select(PolledRow)]
        );
    }

    public static OwnerPollDiagnostics PolledRow(string owner) =>
        new(
            new OwnerPollWindow(owner, StartedAt, StartedAt.AddSeconds(2)),
            new OwnerPollOutcome(OwnerFetchStatus.Ok, resolvedLogin: "octocat"),
            FetchCounts.Fetched(requested: 12, reviewed: 8, union: 15, derived: 4),
            new ExclusionCounts(Draft: 6, ClosedOrMerged: 2, Approved: 3, Untracked: 0),
            new RateLimitReading(4987, StartedAt.AddHours(1), 13),
            new ContributedPullRequests([$"{owner}/api#12", "ps-unite/tools#3"], 1)
        );

    public static OwnerPollDiagnostics CarriedOverRow(string owner) =>
        new(
            new OwnerPollWindow(owner, StartedAt, StartedAt.AddSeconds(3)),
            new OwnerPollOutcome(OwnerFetchStatus.Error, "The owner's queue could not be fetched."),
            FetchCounts.NothingFetched(carriedOver: 0),
            exclusions: null,
            rateLimit: null,
            new ContributedPullRequests([], 0)
        );

    public static OwnerPollDiagnostics UnreachedRow(string owner) =>
        new(
            new OwnerPollWindow(owner),
            new OwnerPollOutcome(OwnerFetchStatus.NotPolled),
            counts: null,
            exclusions: null,
            rateLimit: null,
            ContributedPullRequests.None
        );
}
