using PrCenter.Core.Diagnostics;
using PrCenter.Core.Ports;

namespace PrCenter.Web.Tests.Settings;

/// <summary>
/// Builds <see cref="PollDiagnostics"/> records for the diagnostics view tests.
/// </summary>
internal static class DiagnosticsRecords
{
    public static readonly DateTimeOffset At = new(2026, 7, 29, 14, 5, 12, TimeSpan.Zero);

    /// <summary>A successful poll over the named owners, each polled and consistent.</summary>
    public static PollDiagnostics Poll(
        DateTimeOffset? startedAt = null,
        PollOutcome outcome = PollOutcome.Succeeded,
        int? publishedCount = 12,
        IReadOnlyList<string>? configuredOwners = null,
        IReadOnlyList<OwnerPollDiagnostics>? owners = null
    )
    {
        var configured = configuredOwners ?? ["acme"];
        return new PollDiagnostics(
            new PollRunDiagnostics(
                Guid.NewGuid(),
                startedAt ?? At,
                (startedAt ?? At).AddSeconds(3),
                outcome,
                configured,
                publishedCount
            ),
            owners ?? [.. configured.Select(owner => Polled(owner))]
        );
    }

    /// <summary>A poll whose configured owners were never enumerated.</summary>
    public static PollDiagnostics WithoutConfiguredOwners() =>
        new(new PollRunDiagnostics(Guid.NewGuid(), At, At.AddSeconds(1), PollOutcome.Faulted), []);

    public static OwnerPollDiagnostics Polled(
        string owner,
        int derived = 12,
        int foreignCount = 0,
        IReadOnlyList<string>? ids = null
    ) =>
        new(
            new OwnerPollWindow(owner, At, At.AddSeconds(2)),
            new OwnerPollOutcome(OwnerFetchStatus.Ok, resolvedLogin: "octocat"),
            FetchCounts.Fetched(requested: 12, reviewed: 8, union: 15, derived: derived),
            new ExclusionCounts(Draft: 6, ClosedOrMerged: 2, Approved: 3, Untracked: 0),
            new RateLimitReading(4987, At.AddHours(1), 13),
            new ContributedPullRequests(ids ?? [$"{owner}/api#12"], foreignCount)
        );

    public static OwnerPollDiagnostics Unreached(string owner) =>
        new(
            new OwnerPollWindow(owner),
            new OwnerPollOutcome(OwnerFetchStatus.NotPolled),
            counts: null,
            exclusions: null,
            rateLimit: null,
            ContributedPullRequests.None
        );

    public static OwnerPollDiagnostics Failed(string owner, int carriedOver = 5) =>
        new(
            new OwnerPollWindow(owner, At, At.AddSeconds(1)),
            new OwnerPollOutcome(OwnerFetchStatus.MisconfiguredToken, "The token was rejected."),
            FetchCounts.NothingFetched(carriedOver),
            exclusions: null,
            rateLimit: null,
            ContributedPullRequests.None
        );
}
