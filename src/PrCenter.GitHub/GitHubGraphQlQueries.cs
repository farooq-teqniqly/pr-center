namespace PrCenter.GitHub;

/// <summary>
/// The GraphQL query documents the adapter sends. Kept as the single
/// shape-aware string alongside the mapper that reads the response, so schema
/// changes touch one place.
/// </summary>
internal static class GitHubGraphQlQueries
{
    /// <summary>
    /// Discovery query: two aliased searches (`requested`, `reviewed`) whose
    /// values are supplied as variables, each returning the nested pull-request
    /// facts. The union and dedupe happen client-side.
    /// </summary>
    public const string ReviewQueue = """
        query($requested: String!, $reviewed: String!) {
          requested: search(query: $requested, type: ISSUE, first: 50) {
            nodes { ...prFacts }
          }
          reviewed: search(query: $reviewed, type: ISSUE, first: 50) {
            nodes { ...prFacts }
          }
        }
        fragment prFacts on PullRequest {
          id
          number
          title
          url
          isDraft
          state
          updatedAt
          author { login }
          repository { name owner { login } }
          reviewRequests(first: 50) {
            nodes { requestedReviewer { __typename ... on User { login } } }
          }
          reviews(first: 100) {
            nodes { author { __typename login } state submittedAt }
          }
          commits(last: 100) {
            nodes { commit { committedDate author { user { login } email name } } }
          }
          comments(last: 100) {
            nodes { author { __typename login } createdAt }
          }
          reviewThreads(first: 100) {
            nodes { comments(first: 100) { nodes { author { __typename login } createdAt } } }
          }
        }
        """;
}
