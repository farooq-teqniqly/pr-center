namespace PrCenter.Web.Polling;

/// <summary>
/// Options for the background poll loop, bound from the "Polling" configuration
/// section. Only the interval is configurable until the settings change owns it.
/// </summary>
internal sealed class PollingOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Polling";

    /// <summary>
    /// Gets or sets the interval between scheduled polls. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
}
