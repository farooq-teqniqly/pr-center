namespace PrCenter.Core.Settings;

/// <summary>
/// The interval between scheduled polls of the review queue, constrained to the
/// allowed range of <see cref="Min"/> to <see cref="Max"/> inclusive. Taking and
/// returning this type at the settings boundary makes an out-of-range interval
/// unrepresentable once past validation; a value read from storage that falls
/// outside the range is brought back in by <see cref="Clamp"/> rather than
/// throwing, since the only surface that can correct a stored value lives inside
/// the running app.
/// </summary>
/// <remarks>
/// The default value of this struct carries a zero interval and does not satisfy
/// the range invariant. Obtain an instance through the constructor,
/// <see cref="Clamp"/>, or <see cref="Default"/> -- never through
/// <see langword="default"/>.
/// </remarks>
public readonly record struct PollInterval
{
    /// <summary>The smallest allowed poll interval.</summary>
    public static readonly TimeSpan Min = TimeSpan.FromMinutes(5);

    /// <summary>The largest allowed poll interval.</summary>
    public static readonly TimeSpan Max = TimeSpan.FromHours(24);

    /// <summary>
    /// Initializes a new instance of the <see cref="PollInterval"/> struct.
    /// </summary>
    /// <param name="value">The interval between scheduled polls.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is below <see cref="Min"/> or above <see cref="Max"/>.
    /// </exception>
    public PollInterval(TimeSpan value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, Min);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, Max);

        Value = value;
    }

    /// <summary>
    /// Gets the interval used when no interval has been stored.
    /// </summary>
    public static PollInterval Default { get; } = new(Min);

    /// <summary>Gets the interval between scheduled polls.</summary>
    public TimeSpan Value { get; }

    /// <summary>
    /// Brings a value into the allowed range: returns <see cref="Min"/> for
    /// anything below it, <see cref="Max"/> for anything above it, and the value
    /// itself when it already falls within the range. Used on the read path so a
    /// stored value edited outside the app degrades to a usable interval instead
    /// of making the app unbootable.
    /// </summary>
    /// <param name="value">The value to bring into range.</param>
    /// <returns>The nearest interval within the allowed range.</returns>
    public static PollInterval Clamp(TimeSpan value)
    {
        if (value < Min)
        {
            return new PollInterval(Min);
        }

        return value > Max ? new PollInterval(Max) : new PollInterval(value);
    }
}
