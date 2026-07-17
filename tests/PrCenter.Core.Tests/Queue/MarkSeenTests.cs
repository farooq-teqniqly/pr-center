namespace PrCenter.Core.Tests.Queue;

using NSubstitute;
using PrCenter.Core.Facts;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;

public sealed class MarkSeenTests
{
    private const string Owner = "PerfectServe";
    private const string Repository = "repo";
    private const int Number = 1;
    private const string PullRequestId = "PerfectServe/repo#1";

    private static readonly DateTimeOffset Base = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    private readonly IGitHubFacts _facts = Substitute.For<IGitHubFacts>();
    private readonly IStateStore _stateStore = Substitute.For<IStateStore>();

    [Fact]
    public async Task MarkSeenAsync_WithActivity_WritesMarkerAtMaxTimestampIncludingOwnAndBotEvents()
    {
        // Arrange
        var facts = FactsWith(
            new PullRequestActivity(
                [],
                [new ReviewFact(TestLogins.Other, ReviewState.Approved, Base.AddHours(2))],
                [new CommitFact(TestLogins.Me, Base.AddHours(1))],
                [new CommentFact("dependabot", Base.AddHours(3), isBot: true)]
            )
        );
        StubFetch(facts);

        // Act
        await CreateMarkSeen()
            .MarkSeenAsync(Owner, Repository, Number, PullRequestId, CancellationToken.None);

        // Assert
        await _stateStore
            .Received(1)
            .SetLastSeenAsync(PullRequestId, Base.AddHours(3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkSeenAsync_WhenLiveFetchReturnsNull_WritesNoMarker()
    {
        // Arrange
        _facts
            .GetPullRequestFactsAsync(Owner, Repository, Number, Arg.Any<CancellationToken>())
            .Returns((PullRequestFacts?)null);

        // Act
        await CreateMarkSeen()
            .MarkSeenAsync(Owner, Repository, Number, PullRequestId, CancellationToken.None);

        // Assert
        await _stateStore
            .DidNotReceive()
            .SetLastSeenAsync(
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task MarkSeenAsync_WithNoActivityEvents_FallsBackToLastTouchStamp()
    {
        // Arrange
        var facts = FactsWith(new PullRequestActivity([], [], [], []));
        StubFetch(facts);

        // Act
        await CreateMarkSeen()
            .MarkSeenAsync(Owner, Repository, Number, PullRequestId, CancellationToken.None);

        // Assert
        await _stateStore
            .Received(1)
            .SetLastSeenAsync(PullRequestId, Base, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MarkSeenAsync_NullOrWhitespaceOwner_Throws(string? owner)
    {
        // Arrange
        var markSeen = CreateMarkSeen();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            markSeen.MarkSeenAsync(
                owner!,
                Repository,
                Number,
                PullRequestId,
                CancellationToken.None
            )
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MarkSeenAsync_NullOrWhitespaceRepository_Throws(string? repository)
    {
        // Arrange
        var markSeen = CreateMarkSeen();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            markSeen.MarkSeenAsync(
                Owner,
                repository!,
                Number,
                PullRequestId,
                CancellationToken.None
            )
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MarkSeenAsync_NullOrWhitespacePullRequestId_Throws(string? pullRequestId)
    {
        // Arrange
        var markSeen = CreateMarkSeen();

        // Act / Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            markSeen.MarkSeenAsync(
                Owner,
                Repository,
                Number,
                pullRequestId!,
                CancellationToken.None
            )
        );
    }

    private MarkSeen CreateMarkSeen() => new(_facts, _stateStore);

    private void StubFetch(PullRequestFacts facts) =>
        _facts
            .GetPullRequestFactsAsync(Owner, Repository, Number, Arg.Any<CancellationToken>())
            .Returns(facts);

    private static PullRequestFacts FactsWith(PullRequestActivity activity) =>
        new(
            new PullRequestIdentity(
                PullRequestId,
                Owner,
                Repository,
                Number,
                "title",
                "https://github.com/PerfectServe/repo/pull/1",
                TestLogins.Author
            ),
            new PullRequestStatus(
                isDraft: false,
                isClosedOrMerged: false,
                lastUpdatedBy: "author",
                lastUpdatedAt: Base
            ),
            activity
        );
}
