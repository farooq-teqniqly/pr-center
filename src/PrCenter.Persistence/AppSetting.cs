namespace PrCenter.Persistence;

/// <summary>
/// The single-row record holding the application settings that are not secret --
/// currently just the poll interval. The absence of this row means "no interval
/// stored", which the store reads as the default rather than as an error, so no
/// row is seeded at migration time.
/// </summary>
internal sealed class AppSetting
{
    /// <summary>The fixed primary key of the single app-settings row.</summary>
    public const int SingletonId = 1;

    /// <summary>Gets or sets the fixed single-row primary key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the interval between scheduled polls, in seconds. Stored as a
    /// whole number of seconds rather than as a formatted duration string, so the
    /// value stays unambiguous to anyone inspecting or repairing the SQLite file
    /// directly.
    /// </summary>
    public long PollIntervalSeconds { get; set; }
}
