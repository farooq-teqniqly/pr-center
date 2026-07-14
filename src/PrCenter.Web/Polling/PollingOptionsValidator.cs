using Microsoft.Extensions.Options;

namespace PrCenter.Web.Polling;

/// <summary>
/// Validates that the configured poll interval falls within the allowed range
/// (<see cref="PollingOptions.MinInterval"/> to <see cref="PollingOptions.MaxInterval"/>,
/// inclusive), so a misconfigured interval fails fast at startup rather than
/// producing a runaway or effectively-disabled poll loop.
/// </summary>
internal sealed class PollingOptionsValidator : IValidateOptions<PollingOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PollingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Interval < PollingOptions.MinInterval)
        {
            return ValidateOptionsResult.Fail(
                $"Polling interval {options.Interval} is below the minimum {PollingOptions.MinInterval}."
            );
        }

        if (options.Interval > PollingOptions.MaxInterval)
        {
            return ValidateOptionsResult.Fail(
                $"Polling interval {options.Interval} is above the maximum {PollingOptions.MaxInterval}."
            );
        }

        return ValidateOptionsResult.Success;
    }
}
