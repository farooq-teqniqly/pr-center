# Proposal: add-github-adapter

## Why

The queue derivation (roadmap #1, archived) is pure logic waiting for real
inputs: `IGitHubFacts` still has one stub method and nothing produces
`PullRequestFacts`. This change implements the GitHub adapter against the live
API so the polling change (#5) has facts to feed the derivers. Every design
fork this change depends on was settled by the
[2026-07-10 spike](../../../docs/spikes/2026-07-10-github-adapter-spike.md)
against real payloads: GraphQL over REST (1 rate-limit point per nested query
vs ~1,800 REST calls/hour), two-query discovery, PAT-per-owner validated, no
SSO-specific handling needed. It also carries the data and deriver sides of
the bot/CI decision recorded in the idea/state docs on 2026-07-10.

## What Changes

- **Finalize the `IGitHubFacts` port**: fetch all review-relevant pull
  requests for one owner (returning facts plus a per-owner fetch status),
  fetch a single pull request fresh (for #5's MarkSeen live fetch), and the
  existing authenticated-login lookup.
- **Implement the adapter in `PrCenter.GitHub`** with a hand-rolled
  `HttpClient` and one GraphQL query document (no client library; octokit.net
  is REST-only):
  - Discovery per owner: `review-requested:{me}` + `reviewed-by:{me}` search
    queries, unioned and deduplicated by PR id -- the requested-only query
    misses every `AwaitingReReview` candidate (spike P3).
  - Nested detail in the same query: review requests, reviews (dismissed
    filtered out per the recorded decision), commits with committed date and
    author identity, comments (issue + inline; review bodies arrive as
    reviews).
  - One fine-grained PAT per owner (resource owner = that owner), obtained
    via the Core `ITokenVault` port.
  - Rate-limit awareness and a per-owner fetch status: ok / error /
    misconfigured token (no needs-SSO state -- spike P1/P7).
- **Extend the facts model (additive)**: actor-type field so derivers can
  apply the bot policy (bot detection by API actor type, never login text --
  spike P4), and commit author identity that tolerates null user objects on
  unlinked emails (spike P5) via a defined fallback.
- **Amend the derivers to honor the bot decision**: bot comments and reviews
  never raise `HasUpdate`; bot reviews never set `IsAlreadyCovered`; bot
  commits still count. Step 3 of the sequencing recorded in the archived
  add-queue-derivation design (OQ4); the policy already lives in the
  idea/state docs.
- **Settle the GitHub half of issue #6**: the `GitHubFactsClient` stub
  `NotImplementedException` test is deleted as real behavior lands (replaced
  by real red-green tests), and every real member gets its null/whitespace
  guards with guard tests in the same change. The Persistence half of the
  issue stays open for #3/#4.

## Non-goals

- **No polling loop, no `RefreshQueue`/`GetQueue`/`MarkSeen` orchestration** --
  that is add-polling-and-refresh (#5). This change delivers the port and its
  implementation, not the schedule that calls it.
- **No token entry, storage, or crypto** -- `ITokenVault` stays a stub until
  add-token-vault-and-lock (#4); tests fake it. No settings UI (#7).
- **No state store work** -- last-seen markers remain #3's concern; the
  adapter never reads or writes markers.
- **No UI changes** and, per the standing invariant, no PR mutation of any
  kind.
- **No "my emails" configuration.** The spike found the user's own
  unlinked-email commits could falsely flag `HasUpdate`; the mitigation
  decision (config vs accept rarity) is made in this change's design, but any
  settings storage it implies would land with #3/#7.

## Capabilities

### New Capabilities

- `github-adapter`: the `IGitHubFacts` member set and the GraphQL
  implementation -- discovery union, nested detail mapping onto
  `PullRequestFacts`, dismissed-review filtering, PAT-per-owner usage,
  rate-limit handling, and the per-owner fetch status model.

### Modified Capabilities

- `github-facts-model`: additive actor-type on review/comment facts, commit
  author identity fallback semantics for null user objects.
- `queue-derivation`: `UpdateDetector` ignores bot comments/reviews;
  `CoveredFlag` counts only human reviews. Bot commits keep counting.

## Impact

- `PrCenter.GitHub` gains the real client; `PrCenter.Core` gains the facts
  additions, the fetch-status type, and the deriver amendments; tests in the
  sibling test projects (fake `HttpClient` handler pattern from the baseline
  for the adapter -- no live API calls in CI).
- `add-polling-and-refresh` (#5) consumes the finalized port; `add-state-store`
  (#3) is untouched and can proceed in parallel.
- The queue-derivation baseline spec receives its deferred MODIFIED
  requirements (bot policy), keeping spec and implementation in one change.
