namespace PrCenter.GitHub;

using System.Text.Json;
using PrCenter.Core.Facts;

/// <summary>
/// Maps a GraphQL pull-request node onto <see cref="PullRequestFacts"/>. The
/// single component that knows the response shape (see the mapping decisions in
/// the add-github-adapter design): bot flags come from the actor `__typename`,
/// dismissed and pending reviews are dropped, commit land-dates come from
/// `committedDate`, and commit author identity falls back login -> email -> name.
/// </summary>
internal static class PullRequestFactsMapper
{
    /// <summary>
    /// Maps one pull-request node.
    /// </summary>
    /// <param name="pr">The pull-request JSON node.</param>
    /// <returns>The mapped facts.</returns>
    public static PullRequestFacts MapPullRequest(JsonElement pr)
    {
        var reviews = MapReviews(pr);
        var commits = MapCommits(pr);
        var comments = MapComments(pr);
        var requestedLogins = MapRequestedReviewerLogins(pr);

        var repository = pr.GetProperty("repository");
        var identity = new PullRequestIdentity(
            id: RequireString(pr, "id"),
            owner: RequireString(repository.GetProperty("owner"), "login"),
            repository: RequireString(repository, "name"),
            number: pr.GetProperty("number").GetInt32(),
            title: RequireString(pr, "title"),
            url: RequireString(pr, "url")
        );

        var (lastUpdatedBy, lastUpdatedAt) = LatestUpdate(pr, reviews, commits, comments);
        var status = new PullRequestStatus(
            isDraft: pr.GetProperty("isDraft").GetBoolean(),
            isClosedOrMerged: !IsOpen(pr),
            lastUpdatedBy: lastUpdatedBy,
            lastUpdatedAt: lastUpdatedAt
        );

        var activity = new PullRequestActivity(requestedLogins, reviews, commits, comments);
        return new PullRequestFacts(identity, status, activity);
    }

    private static IReadOnlyList<ReviewFact> MapReviews(JsonElement pr)
    {
        var reviews = new List<ReviewFact>();

        foreach (var node in Nodes(pr, "reviews"))
        {
            if (
                MapReviewState(GetString(node, "state")) is not { } state
                || OptProp(node, "author") is not { } author
                || GetString(author, "login") is not { } login
            )
            {
                continue;
            }

            var submittedAt = node.GetProperty("submittedAt").GetDateTimeOffset();
            reviews.Add(new ReviewFact(login, state, submittedAt, IsBot(author)));
        }

        return reviews;
    }

    private static IReadOnlyList<CommitFact> MapCommits(JsonElement pr)
    {
        var commits = new List<CommitFact>();

        foreach (var node in Nodes(pr, "commits"))
        {
            var commit = node.GetProperty("commit");
            var landedAt = commit.GetProperty("committedDate").GetDateTimeOffset();
            commits.Add(new CommitFact(CommitAuthorIdentity(commit), landedAt));
        }

        return commits;
    }

    private static IReadOnlyList<CommentFact> MapComments(JsonElement pr)
    {
        var comments = new List<CommentFact>();

        foreach (var node in Nodes(pr, "comments"))
        {
            AddComment(comments, node);
        }

        foreach (var thread in Nodes(pr, "reviewThreads"))
        {
            foreach (var node in Nodes(thread, "comments"))
            {
                AddComment(comments, node);
            }
        }

        return comments;
    }

    private static void AddComment(List<CommentFact> comments, JsonElement node)
    {
        if (
            OptProp(node, "author") is not { } author
            || GetString(author, "login") is not { } login
        )
        {
            return;
        }

        var createdAt = node.GetProperty("createdAt").GetDateTimeOffset();
        comments.Add(new CommentFact(login, createdAt, IsBot(author)));
    }

    private static IReadOnlyList<string> MapRequestedReviewerLogins(JsonElement pr)
    {
        var logins = new List<string>();

        foreach (var node in Nodes(pr, "reviewRequests"))
        {
            if (
                OptProp(node, "requestedReviewer") is { } reviewer
                && IsTypename(reviewer, "User")
                && GetString(reviewer, "login") is { } login
            )
            {
                logins.Add(login);
            }
        }

        return logins;
    }

    private static string CommitAuthorIdentity(JsonElement commit)
    {
        var author = commit.GetProperty("author");

        if (OptProp(author, "user") is { } user && GetString(user, "login") is { } login)
        {
            return login;
        }

        return GetString(author, "email") ?? GetString(author, "name") ?? "unknown";
    }

    private static (string By, DateTimeOffset At) LatestUpdate(
        JsonElement pr,
        IReadOnlyList<ReviewFact> reviews,
        IReadOnlyList<CommitFact> commits,
        IReadOnlyList<CommentFact> comments
    )
    {
        var events = reviews
            .Select(review => (By: review.ReviewerLogin, At: review.SubmittedAt))
            .Concat(commits.Select(commit => (By: commit.AuthorLogin, At: commit.LandedAt)))
            .Concat(comments.Select(comment => (By: comment.AuthorLogin, At: comment.CreatedAt)))
            .OrderByDescending(entry => entry.At)
            .ToList();

        if (events.Count > 0)
        {
            return events[0];
        }

        var author = OptProp(pr, "author") is { } a ? GetString(a, "login") : null;
        return (author ?? "unknown", pr.GetProperty("updatedAt").GetDateTimeOffset());
    }

    private static ReviewState? MapReviewState(string? state) =>
        state switch
        {
            "APPROVED" => ReviewState.Approved,
            "CHANGES_REQUESTED" => ReviewState.ChangesRequested,
            "COMMENTED" => ReviewState.Commented,
            _ => null,
        };

    private static bool IsOpen(JsonElement pr) =>
        string.Equals(GetString(pr, "state"), "OPEN", StringComparison.Ordinal);

    private static bool IsBot(JsonElement author) => IsTypename(author, "Bot");

    private static bool IsTypename(JsonElement actor, string typename) =>
        string.Equals(GetString(actor, "__typename"), typename, StringComparison.Ordinal);

    private static string RequireString(JsonElement element, string property) =>
        GetString(element, property)
        ?? throw new InvalidOperationException($"Missing required '{property}' in GitHub payload.");

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement? OptProp(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value
            : null;

    private static IEnumerable<JsonElement> Nodes(JsonElement parent, string connection)
    {
        if (
            parent.TryGetProperty(connection, out var conn)
            && conn.ValueKind == JsonValueKind.Object
            && conn.TryGetProperty("nodes", out var nodes)
            && nodes.ValueKind == JsonValueKind.Array
        )
        {
            return nodes.EnumerateArray();
        }

        return [];
    }
}
