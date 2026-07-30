using System.Globalization;
using System.Text;
using PrCenter.Core.Diagnostics;

namespace PrCenter.Web.Components.Settings;

/// <summary>
/// Composes the plain-text form of the recorded polls, for pasting into an issue
/// or a chat while debugging.
/// </summary>
/// <remarks>
/// Redaction is by construction rather than by filtering: the diagnostics record
/// carries only identifiers, counts, instants, and system-composed details, so
/// there is no title, body, comment, URL, or token here to leave out. This type
/// writes fields from that record and composes nothing of its own, which is what
/// keeps that guarantee true as the record grows.
/// </remarks>
internal static class PollDiagnosticsText
{
    /// <summary>
    /// Composes the recorded polls as text, one poll per block.
    /// </summary>
    /// <param name="polls">The summarized polls to render.</param>
    /// <returns>The composed text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="polls"/> is null.</exception>
    public static string Compose(IReadOnlyList<PollSummaryView> polls)
    {
        ArgumentNullException.ThrowIfNull(polls);

        var text = new StringBuilder();
        foreach (var summary in polls)
        {
            AppendPoll(text, summary);
        }

        return text.ToString();
    }

    private static void AppendPoll(StringBuilder text, PollSummaryView summary)
    {
        var run = summary.Poll.Run;
        text.AppendLine(
            CultureInfo.InvariantCulture,
            $"poll {run.PollId} {Instant(run.StartedAt)} -> {Instant(run.CompletedAt)} {run.Outcome} owners {summary.PolledOwners}/{Configured(run)} published {Number(run.PublishedCount)} derived-total {summary.DerivedTotal}"
        );

        foreach (var row in summary.Poll.Owners)
        {
            AppendOwner(text, row);
        }
    }

    private static void AppendOwner(StringBuilder text, OwnerPollDiagnostics row)
    {
        text.AppendLine(
            CultureInfo.InvariantCulture,
            $"  {row.Window.Owner} {row.Outcome.Status} started {Instant(row.Window.StartedAt)} {Counts(row)} foreign {row.Contributed.ForeignCount}"
        );

        foreach (var id in row.Contributed.Ids)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"    {id}");
        }
    }

    private static string Counts(OwnerPollDiagnostics row)
    {
        if (row.Counts is not { } counts)
        {
            return "counts --";
        }

        var exclusions = row.Exclusions is { } tally
            ? $" draft {tally.Draft} closed {tally.ClosedOrMerged} approved {tally.Approved} untracked {tally.Untracked}"
            : string.Empty;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"requested {Number(counts.Requested)} reviewed {Number(counts.Reviewed)} union {Number(counts.Union)} derived {Number(counts.Derived)} carried {counts.CarriedOver}{exclusions}"
        );
    }

    private static string Configured(PollRunDiagnostics run) =>
        run.ConfiguredOwners is { } owners
            ? owners.Count.ToString(CultureInfo.InvariantCulture)
            : "--";

    // Absent stays visibly absent rather than becoming a zero, so the pasted text
    // makes the same distinction the stored record does.
    private static string Number(int? value) =>
        value is { } present ? present.ToString(CultureInfo.InvariantCulture) : "--";

    private static string Instant(DateTimeOffset? at) =>
        at is { } present
            ? present.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : "--";
}
