namespace PrCenter.Web.Tests;

using PrCenter.Web.Polling;

public sealed class PollingOptionsValidatorTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(60, true)]
    [InlineData(1440, true)]
    [InlineData(1441, false)]
    public void Validate_IntervalAgainstAllowedRange_SucceedsOnlyWithinFiveMinutesToTwentyFourHours(
        int minutes,
        bool expectedValid
    )
    {
        // Arrange
        var validator = new PollingOptionsValidator();
        var options = new PollingOptions { Interval = TimeSpan.FromMinutes(minutes) };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.Equal(expectedValid, result.Succeeded);
    }

    [Fact]
    public void Validate_NullOptions_Throws()
    {
        // Arrange
        var validator = new PollingOptionsValidator();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => validator.Validate(name: null, options: null!));
    }
}
