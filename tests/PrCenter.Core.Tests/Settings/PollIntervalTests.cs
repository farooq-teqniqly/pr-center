using PrCenter.Core.Settings;

namespace PrCenter.Core.Tests.Settings;

public sealed class PollIntervalTests
{
    public static TheoryData<int> InRangeMinutes => new() { 5, 5 + 1, 60, (24 * 60) - 1, 24 * 60 };

    public static TheoryData<int> OutOfRangeMinutes =>
        new() { 0, 1, 5 - 1, (24 * 60) + 1, 48 * 60 };

    [Theory]
    [MemberData(nameof(InRangeMinutes))]
    public void Constructor_InRangeValue_RoundTripsTheValue(int minutes)
    {
        // Arrange
        var value = TimeSpan.FromMinutes(minutes);

        // Act
        var interval = new PollInterval(value);

        // Assert
        Assert.Equal(value, interval.Value);
    }

    [Theory]
    [MemberData(nameof(OutOfRangeMinutes))]
    public void Constructor_OutOfRangeValue_Throws(int minutes)
    {
        // Arrange
        var value = TimeSpan.FromMinutes(minutes);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new PollInterval(value));
    }

    [Fact]
    public void Constructor_NegativeValue_Throws()
    {
        // Arrange
        var value = TimeSpan.FromMinutes(-5);

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new PollInterval(value));
    }

    [Fact]
    public void Clamp_ValueBelowMinimum_ReturnsMinimum()
    {
        // Arrange
        var value = TimeSpan.FromSeconds(1);

        // Act
        var clamped = PollInterval.Clamp(value);

        // Assert
        Assert.Equal(PollInterval.Min, clamped.Value);
    }

    [Fact]
    public void Clamp_ValueAboveMaximum_ReturnsMaximum()
    {
        // Arrange
        var value = TimeSpan.FromDays(7);

        // Act
        var clamped = PollInterval.Clamp(value);

        // Assert
        Assert.Equal(PollInterval.Max, clamped.Value);
    }

    [Theory]
    [MemberData(nameof(InRangeMinutes))]
    public void Clamp_InRangeValue_ReturnsTheValueUnchanged(int minutes)
    {
        // Arrange
        var value = TimeSpan.FromMinutes(minutes);

        // Act
        var clamped = PollInterval.Clamp(value);

        // Assert
        Assert.Equal(value, clamped.Value);
    }

    [Fact]
    public void Default_NoStoredInterval_IsFiveMinutes()
    {
        // Arrange / Act
        var fallback = PollInterval.Default;

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), fallback.Value);
    }

    [Fact]
    public void Equals_SameValue_AreEqual()
    {
        // Arrange
        var first = new PollInterval(TimeSpan.FromMinutes(30));
        var second = new PollInterval(TimeSpan.FromMinutes(30));

        // Act
        var equal = first == second;

        // Assert
        Assert.True(equal);
    }
}
