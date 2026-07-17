using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Derivation;

internal static class TestFacts
{
    public static PullRequestFacts Create(
        bool isDraft = false,
        bool isClosedOrMerged = false,
        IReadOnlyList<string>? requested = null,
        IReadOnlyList<ReviewFact>? reviews = null,
        IReadOnlyList<CommitFact>? commits = null,
        IReadOnlyList<CommentFact>? comments = null
    ) =>
        new(
            new PullRequestIdentity(
                id: "owner/repo#1",
                owner: "owner",
                repository: "repo",
                number: 1,
                title: "Add feature",
                url: "https://github.com/owner/repo/pull/1",
                authorLogin: TestLogins.Author
            ),
            new PullRequestStatus(
                isDraft: isDraft,
                isClosedOrMerged: isClosedOrMerged,
                lastUpdatedBy: TestLogins.Author,
                lastUpdatedAt: TestTime.At(1)
            ),
            new PullRequestActivity(requested ?? [], reviews ?? [], commits ?? [], comments ?? [])
        );
}
