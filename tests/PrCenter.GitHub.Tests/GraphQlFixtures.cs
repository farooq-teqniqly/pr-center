namespace PrCenter.GitHub.Tests;

/// <summary>
/// Canned GraphQL response bodies shaped from the 2026-07-10 spike payloads,
/// covering the mapping edge cases: a PR in both searches (union dedupe), a bot
/// review and bot inline comment (`__typename`: Bot), a dismissed review, a
/// commit whose author user object is null (email fallback), a draft PR, and a
/// reviewed-by-only PR.
/// </summary>
internal static class GraphQlFixtures
{
    /// <summary>The owner login the review-queue fixture belongs to.</summary>
    public const string Owner = "acme";

    /// <summary>A successful response with both searches empty (no PRs for the user).</summary>
    public const string EmptyResultsResponse = """
        { "data": { "requested": { "nodes": [] }, "reviewed": { "nodes": [] } } }
        """;

    /// <summary>A 200 GraphQL payload carrying a FORBIDDEN error (token lacks permission).</summary>
    public const string ForbiddenErrorsResponse = """
        { "errors": [ { "type": "FORBIDDEN", "message": "Resource not accessible by personal access token" } ] }
        """;

    /// <summary>A 200 GraphQL payload carrying a RATE_LIMITED error (primary rate limit exhausted).</summary>
    public const string RateLimitedErrorsResponse = """
        { "errors": [ { "type": "RATE_LIMITED", "message": "API rate limit exceeded" } ] }
        """;

    /// <summary>A single-pull-request response for a merged PR.</summary>
    public const string MergedPullRequestResponse = """
        {
          "data": { "repository": { "pullRequest": {
            "id": "M", "number": 9, "title": "Merged PR", "url": "https://github.com/acme/repo/pull/9",
            "isDraft": false, "state": "MERGED", "updatedAt": "2026-07-05T10:00:00Z",
            "author": { "login": "author-m" },
            "repository": { "name": "repo", "owner": { "login": "acme" } },
            "reviewRequests": { "nodes": [] },
            "reviews": { "nodes": [] },
            "commits": { "nodes": [] },
            "comments": { "nodes": [] },
            "reviewThreads": { "nodes": [] }
          } } }
        }
        """;

    /// <summary>A single-pull-request response where the PR does not exist.</summary>
    public const string MissingPullRequestResponse = """
        { "data": { "repository": { "pullRequest": null } } }
        """;

    /// <summary>A viewer response resolving the authenticated login.</summary>
    public const string ViewerResponse = """
        { "data": { "viewer": { "login": "octocat" } } }
        """;

    /// <summary>Well-formed JSON whose review-queue shape is wrong (a null search alias).</summary>
    public const string WrongShapeReviewQueueResponse = """
        { "data": { "requested": null, "reviewed": { "nodes": [] } } }
        """;

    /// <summary>Well-formed JSON whose single-PR node is missing required fields.</summary>
    public const string WrongShapeSinglePullRequestResponse = """
        { "data": { "repository": { "pullRequest": { "id": "X" } } } }
        """;

    /// <summary>
    /// A response with a single PR exercising the mapping edge branches: an
    /// approved review, commits whose author user is null with the email then
    /// name then a final "unknown" fallback, a comment with a null author
    /// (skipped), and an omitted `reviewThreads` connection (empty-nodes path).
    /// </summary>
    public const string MappingEdgeCasesResponse = """
        {
          "data": {
            "requested": { "nodes": [
              {
                "id": "D", "number": 4, "title": "PR D", "url": "https://github.com/acme/repo-d/pull/4",
                "isDraft": false, "state": "OPEN", "updatedAt": "2026-07-04T10:00:00Z",
                "author": { "login": "author-d" },
                "repository": { "name": "repo-d", "owner": { "login": "acme" } },
                "reviewRequests": { "nodes": [] },
                "reviews": { "nodes": [
                  { "author": { "__typename": "User", "login": "approver" }, "state": "APPROVED", "submittedAt": "2026-07-04T09:00:00Z" }
                ] },
                "commits": { "nodes": [
                  { "commit": { "committedDate": "2026-07-04T08:00:00Z", "author": { "user": { "login": "linked-dev" }, "email": "linked@example.com", "name": "Linked Dev" } } },
                  { "commit": { "committedDate": "2026-07-04T07:00:00Z", "author": { "user": null, "email": null, "name": "Only Name" } } },
                  { "commit": { "committedDate": "2026-07-04T06:00:00Z", "author": { "user": null, "email": null, "name": null } } }
                ] },
                "comments": { "nodes": [
                  { "author": null, "createdAt": "2026-07-04T08:00:00Z" }
                ] }
              }
            ] },
            "reviewed": { "nodes": [] }
          }
        }
        """;

    /// <summary>A successful review-queue response with three distinct PRs (A in both searches).</summary>
    public const string ReviewQueueResponse = """
        {
          "data": {
            "requested": { "nodes": [
              {
                "id": "A", "number": 1, "title": "PR A", "url": "https://github.com/acme/repo-a/pull/1",
                "isDraft": false, "state": "OPEN", "updatedAt": "2026-07-01T10:00:00Z",
                "author": { "login": "author-a" },
                "repository": { "name": "repo-a", "owner": { "login": "acme" } },
                "reviewRequests": { "nodes": [
                  { "requestedReviewer": { "__typename": "User", "login": "octocat" } },
                  { "requestedReviewer": { "__typename": "Team", "name": "team-x" } }
                ] },
                "reviews": { "nodes": [
                  { "author": { "__typename": "User", "login": "human-rev" }, "state": "COMMENTED", "submittedAt": "2026-07-01T09:00:00Z" },
                  { "author": { "__typename": "Bot", "login": "qodo" }, "state": "COMMENTED", "submittedAt": "2026-07-01T09:30:00Z" },
                  { "author": { "__typename": "User", "login": "dismissed-rev" }, "state": "DISMISSED", "submittedAt": "2026-07-01T08:00:00Z" }
                ] },
                "commits": { "nodes": [
                  { "commit": { "committedDate": "2026-07-01T07:00:00Z", "author": { "user": null, "email": "unlinked@example.com", "name": "Unlinked Dev" } } }
                ] },
                "comments": { "nodes": [
                  { "author": { "__typename": "User", "login": "human-commenter" }, "createdAt": "2026-07-01T09:15:00Z" }
                ] },
                "reviewThreads": { "nodes": [
                  { "comments": { "nodes": [
                    { "author": { "__typename": "Bot", "login": "Copilot" }, "createdAt": "2026-07-01T09:45:00Z" }
                  ] } }
                ] }
              },
              {
                "id": "B", "number": 2, "title": "PR B", "url": "https://github.com/acme/repo-b/pull/2",
                "isDraft": true, "state": "OPEN", "updatedAt": "2026-07-02T10:00:00Z",
                "author": { "login": "author-b" },
                "repository": { "name": "repo-b", "owner": { "login": "acme" } },
                "reviewRequests": { "nodes": [] },
                "reviews": { "nodes": [] },
                "commits": { "nodes": [] },
                "comments": { "nodes": [] },
                "reviewThreads": { "nodes": [] }
              }
            ] },
            "reviewed": { "nodes": [
              {
                "id": "A", "number": 1, "title": "PR A", "url": "https://github.com/acme/repo-a/pull/1",
                "isDraft": false, "state": "OPEN", "updatedAt": "2026-07-01T10:00:00Z",
                "author": { "login": "author-a" },
                "repository": { "name": "repo-a", "owner": { "login": "acme" } },
                "reviewRequests": { "nodes": [
                  { "requestedReviewer": { "__typename": "User", "login": "octocat" } },
                  { "requestedReviewer": { "__typename": "Team", "name": "team-x" } }
                ] },
                "reviews": { "nodes": [
                  { "author": { "__typename": "User", "login": "human-rev" }, "state": "COMMENTED", "submittedAt": "2026-07-01T09:00:00Z" },
                  { "author": { "__typename": "Bot", "login": "qodo" }, "state": "COMMENTED", "submittedAt": "2026-07-01T09:30:00Z" },
                  { "author": { "__typename": "User", "login": "dismissed-rev" }, "state": "DISMISSED", "submittedAt": "2026-07-01T08:00:00Z" }
                ] },
                "commits": { "nodes": [
                  { "commit": { "committedDate": "2026-07-01T07:00:00Z", "author": { "user": null, "email": "unlinked@example.com", "name": "Unlinked Dev" } } }
                ] },
                "comments": { "nodes": [
                  { "author": { "__typename": "User", "login": "human-commenter" }, "createdAt": "2026-07-01T09:15:00Z" }
                ] },
                "reviewThreads": { "nodes": [
                  { "comments": { "nodes": [
                    { "author": { "__typename": "Bot", "login": "Copilot" }, "createdAt": "2026-07-01T09:45:00Z" }
                  ] } }
                ] }
              },
              {
                "id": "C", "number": 3, "title": "PR C", "url": "https://github.com/acme/repo-c/pull/3",
                "isDraft": false, "state": "OPEN", "updatedAt": "2026-07-03T10:00:00Z",
                "author": { "login": "author-c" },
                "repository": { "name": "repo-c", "owner": { "login": "acme" } },
                "reviewRequests": { "nodes": [] },
                "reviews": { "nodes": [
                  { "author": { "__typename": "User", "login": "octocat" }, "state": "CHANGES_REQUESTED", "submittedAt": "2026-07-03T09:00:00Z" }
                ] },
                "commits": { "nodes": [] },
                "comments": { "nodes": [] },
                "reviewThreads": { "nodes": [] }
              }
            ] }
          }
        }
        """;
}
