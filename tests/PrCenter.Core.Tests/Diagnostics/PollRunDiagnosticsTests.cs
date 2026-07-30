using PrCenter.Core.Diagnostics;

namespace PrCenter.Core.Tests.Diagnostics;

public sealed class PollRunDiagnosticsTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 7, 29, 14, 5, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithoutConfiguredOwners_LeavesThemAbsent()
    {
        // Act -- absent means the owner enumeration never completed
        var run = new PollRunDiagnostics(
            Guid.NewGuid(),
            StartedAt,
            StartedAt.AddSeconds(3),
            PollOutcome.Faulted
        );

        // Assert
        Assert.Null(run.ConfiguredOwners);
        Assert.Null(run.PublishedCount);
    }

    [Fact]
    public void Constructor_WithNoConfiguredOwners_KeepsTheEmptyListDistinctFromAbsent()
    {
        // Act -- an empty list means no owners are configured, which is a real state
        var run = new PollRunDiagnostics(
            Guid.NewGuid(),
            StartedAt,
            StartedAt.AddSeconds(3),
            PollOutcome.Succeeded,
            configuredOwners: [],
            publishedCount: 0
        );

        // Assert
        Assert.NotNull(run.ConfiguredOwners);
        Assert.Empty(run.ConfiguredOwners);
    }

    [Fact]
    public void Constructor_DoesNotObserveLaterMutationOfConfiguredOwners()
    {
        // Arrange
        var owners = new List<string> { "acme" };
        var run = new PollRunDiagnostics(
            Guid.NewGuid(),
            StartedAt,
            StartedAt.AddSeconds(3),
            PollOutcome.Succeeded,
            owners,
            1
        );

        // Act
        owners.Add("ps-unite");

        // Assert
        Assert.Single(run.ConfiguredOwners!);
    }
}
