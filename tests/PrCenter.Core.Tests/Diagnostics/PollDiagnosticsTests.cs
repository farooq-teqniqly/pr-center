using PrCenter.Core.Diagnostics;

namespace PrCenter.Core.Tests.Diagnostics;

public sealed class PollDiagnosticsTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 7, 29, 14, 5, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithNullRun_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new PollDiagnostics(null!, []));
    }

    [Fact]
    public void Constructor_WithNullOwners_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new PollDiagnostics(Run(), null!));
    }

    [Fact]
    public void Constructor_DoesNotObserveLaterMutationOfSourceList()
    {
        // Arrange
        var owners = new List<OwnerPollDiagnostics> { Row("acme") };
        var record = new PollDiagnostics(Run(), owners);

        // Act
        owners.Add(Row("ps-unite"));

        // Assert
        Assert.Single(record.Owners);
    }

    private static PollRunDiagnostics Run() =>
        new(Guid.NewGuid(), StartedAt, StartedAt.AddSeconds(3), PollOutcome.Succeeded, ["acme"], 1);

    private static OwnerPollDiagnostics Row(string owner) =>
        new(
            new OwnerPollWindow(owner),
            new OwnerPollOutcome(Core.Ports.OwnerFetchStatus.NotPolled),
            counts: null,
            exclusions: null,
            rateLimit: null,
            ContributedPullRequests.None
        );
}
