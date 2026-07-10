namespace PrCenter.Core.Tests.Derivation;

internal static class TestTime
{
    public static DateTimeOffset At(int hour) => new(2026, 1, 1, hour, 0, 0, TimeSpan.Zero);
}
