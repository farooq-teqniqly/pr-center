namespace PrCenter.Web.Components.Queue;

/// <summary>
/// Pure UI helper that formats an instant relative to now for display. No Core
/// state, no derivation -- presentation-only rounding of an elapsed span into a
/// short human string.
/// </summary>
internal static class RelativeTime
{
    /// <summary>
    /// Formats <paramref name="at"/> relative to <paramref name="now"/>.
    /// </summary>
    /// <param name="at">The instant to format.</param>
    /// <param name="now">The instant considered "now".</param>
    /// <returns>A short relative-time string, e.g. "22m ago" or "3d ago".</returns>
    public static string Format(DateTimeOffset at, DateTimeOffset now)
    {
        var elapsed = now - at;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return elapsed switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes}m ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours}h ago",
            { TotalDays: < 7 } => $"{(int)elapsed.TotalDays}d ago",
            _ => at.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture),
        };
    }
}
