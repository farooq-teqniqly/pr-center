# Design: add-github-adapter

## Context

Every load-bearing unknown was settled by the
[2026-07-10 spike](../../../docs/spikes/2026-07-10-github-adapter-spike.md)
against live payloads; this design records the resulting decisions rather than
re-arguing them. The bot/CI policy is already decided in
[pr-center-idea.md](../../../docs/pr-center-idea.md) and
[pr-center-state.md](../../../docs/pr-center-state.md); this change implements
its data and deriver sides. Architecture boundaries per
[pr-center-architecture.md](../../../docs/pr-center-architecture.md): the
adapter lives in `PrCenter.GitHub`, implements Core ports, and never
references the Persistence adapter.

## Goals / Non-Goals

**Goals:**

- Final `IGitHubFacts` member set; real GraphQL implementation behind it.
- Facts model carries what the bot policy and commit-identity reality require.
- Derivers honor the bot decision; adapter filters dismissed reviews.
- Per-owner fetch status that disambiguates "empty" from "broken."

**Non-Goals:**

- No polling schedule, marker reads/writes, token crypto, or UI (see
  proposal non-goals).

## Decisions

### D1: Port member set -- three members, `myLogin` passed in

```text
IGitHubFacts
  GetAuthenticatedUserLoginAsync(owner, ct)                  (exists today)
  GetReviewQueueFactsAsync(owner, myLogin, ct) -> OwnerFactsResult
  GetPullRequestFactsAsync(owner, repository, number, ct) -> PullRequestFacts?
```

- `GetReviewQueueFactsAsync` takes `myLogin` as a parameter instead of
  resolving it internally: the caller (#5's RefreshQueue) already resolves it
  once per owner for the derivers, and passing it keeps the adapter free of
  hidden per-owner state. Same pattern the derivers use.
- `GetPullRequestFactsAsync` is the single-PR fresh fetch #5's MarkSeen needs
  (click-through re-fetches before writing the marker). Returns null when the
  PR is inaccessible or gone; a closed-or-merged PR still returns facts
  (`IsClosedOrMerged` true) so MarkSeen can behave sensibly.
- *Alternative: adapter resolves login itself.* Rejected: duplicate resolution
  and cache invalidation questions for no caller convenience.

### D2: `OwnerFactsResult` and `OwnerFetchStatus` live in Core

`OwnerFetchStatus`: `Ok | MisconfiguredToken | Error`. `OwnerFactsResult`
carries the status, the facts list (empty on failure), and an optional
human-readable detail for the UI's per-owner indicator. No `NeedsSso` state:
org-resource-owner fine-grained PATs require no separate SAML authorization
(spike P1/P7); a wrong-resource-owner PAT surfaces as `MisconfiguredToken`.
An `Ok` with zero facts is the legitimate "no PRs for me here" case the idea
doc requires distinguishing from failure.

### D3: One GraphQL request per owner per poll, aliased searches

Hand-rolled `HttpClient` POST to `/graphql` with a single query document
containing **two aliased search fields** (`review-requested:{me}` and
`reviewed-by:{me}`, both `is:pr is:open org:{owner}` scoped -- for the
personal owner, `user:{owner}`), each with the nested PR selection: review
requests, reviews, commits (committed date + author user/email/name),
issue + inline comments. Union client-side by PR id.

- Discovery must union both qualifiers: requested-only misses every
  `AwaitingReReview` candidate (spike P3, 6 of 7 live PRs).
- Page sizes: searches `first: 50` with cursor follow-up (sanity cap 200 PRs
  per owner); nested collections sized generously (reviews/comments 100,
  commits 100) -- the spike's noisiest PR had 13 reviews from one person, and
  a >100-events-since-last-look PR flags an update long before the cap
  matters. Truncation therefore degrades toward "still flagged," never toward
  a silent miss; accepted for v1.
- `Commit.committedDate` is the land-date mapping for `CommitFact.LandedAt`
  (D5 of the derivation design; spike P5).
- No `is:draft` exclusion in the search: drafts are fetched with
  `isDraft: true` and Core's `MembershipDeriver` excludes them. Derivation
  stays in Core; the nested-query cost of a draft row is negligible at 1
  point per request.
- *Alternative: octokit.net / octokit.graphql.net.* Rejected: REST-only /
  maintenance risk for a single query document (spike transport analysis).

### D4: Adapter obtains PATs through the Core `ITokenVault` port

`GitHubFactsClient` depends on `ITokenVault` (a Core interface -- allowed by
the dependency rules; adapters only may not reference each other) and asks it
for the owner's token per call. No token caching in the adapter; the vault
owns token lifetime. Until #4, the vault stub throws -- adapter tests fake
`ITokenVault` with NSubstitute.

Auth header `Bearer {token}` plus a required `User-Agent`. One `HttpClient`
via `IHttpClientFactory` (named client registered in Web's composition root);
per-owner variation is only the auth header, set per request, so a single
named client suffices.

### D4a: Standard resilience handler on the named client

The named `HttpClient` gets `AddStandardResilienceHandler()` from
`Microsoft.Extensions.Http.Resilience` (Polly v8-based; package version in
`Directory.Packages.props`, referenced by `PrCenter.Web` where the client is
registered -- the adapter never sees the pipeline). Defaults are accepted:
rate-limiter, total-timeout, retry with backoff (handles transient 5xx/408/429
and honors `Retry-After`), circuit breaker, attempt timeout.

Layering with D9 and #5, so retries do not stack up confusingly:

- The resilience handler owns **transient, per-request** recovery (network
  blips, 5xx, secondary-rate-limit 429s). The adapter only sees the final
  outcome.
- D9's status mapping owns turning that final outcome into a per-owner status.
  GraphQL *primary* rate-limit exhaustion arrives as a 200 payload with
  errors, which the handler correctly does not retry -- it maps to `Error`
  and the next poll tick is the retry.
- #5's poll loop owns **cadence-level** recovery (an owner in `Error` simply
  gets retried next tick). No poll-level retry logic belongs here.

Retrying the POST is safe: the GraphQL document is a read-only query
(idempotent by construction; the app never mutates PR state).

### D5: Facts model gains actor-type on reviews and comments only

`ReviewFact` and `CommentFact` gain an `IsBot` flag (GraphQL
`author.__typename == "Bot"`; never login text -- spike P4). `CommitFact`
deliberately does **not**: the decided policy counts bot commits, so no
deriver reads a commit actor type. Additive constructor parameters; existing
guard structure unchanged.

### D6: Commit author identity -- login, else email, else name; no config

`CommitFact.AuthorLogin` maps from `commit.author.user.login` when the email
is linked to a GitHub account; otherwise falls back to `commit.author.email`,
then `commit.author.name` (all in the same query; spike P5 showed the user
object is null for unlinked emails).

The known me-only hole -- the user's own unlinked-email push to a PR they
review would false-flag `HasUpdate` because `fmahmud@perfectserve.com` never
matched `farooq-teqniqly` -- was **closed operationally, not in code**: the
work email was linked to the GitHub account on 2026-07-10 and re-probing the
same commits confirmed attribution now resolves retroactively. The fallback
mapping above remains for *other* unlinked authors, where "treated as someone
else" is the correct outcome anyway. No "my identities" configuration.

- *Alternative: configured "my emails" list threaded into UpdateDetector.*
  Rejected: the only harmful case was the user's own identity, and email
  linking fixed it with zero code.

### D7: Dismissed reviews are filtered in the adapter

The list payload replaces a dismissed review's verdict with `DISMISSED`
(spike P6), so the adapter simply omits them from `ReviewFact`s. Derivers
never see dismissed reviews; no deriver change needed for OQ3. Consistent
with the recorded "treat as withdrawn" decision.

### D8: Deriver amendments for the bot policy

- `UpdateDetector`: comments and reviews with `IsBot` never produce
  has-update; commits are not filtered (bot commits count).
- `CoveredFlag`: only reviews with `IsBot == false` count toward covered.
- `MembershipDeriver`: unchanged -- membership is me-relative and the user is
  not a bot.

### D9: Error-to-status mapping

| Condition | Status |
| --- | --- |
| 200 with data | `Ok` (empty facts list is still `Ok`) |
| 401, or GraphQL `FORBIDDEN`-type errors | `MisconfiguredToken` |
| Rate-limit exhaustion (primary or secondary) | `Error` with detail; backoff/retry cadence belongs to #5's loop |
| Network failure, 5xx, malformed payload | `Error` with detail |

The adapter never throws for per-owner fetch failures -- failures are data
(the status), so one broken owner cannot take down a poll over the others.
Guard-clause violations (null/whitespace arguments) still throw per baseline.

### D10: Issue #6, GitHub half

The `GitHubFactsClient` stub `NotImplementedException` test is deleted when
the method gains real behavior (never ported forward); every real member gets
`ThrowIfNull`/`ThrowIfNullOrWhiteSpace` guards with `<exception>` docs and
guard tests in the same commits. Persistence half explicitly remains open.

### D11: Testing -- fake handler, spike-shaped fixtures

Adapter tests use the baseline's fake-`HttpClient` pattern (public abstract
`MockSendAsync`, sealed `SendAsync` delegating to it) with canned GraphQL
JSON fixtures copied from the spike's real payload shapes (null commit
authors, `__typename: Bot`, `DISMISSED` reviews included). No live API calls
in tests. Deriver amendments are plain unit tests in `PrCenter.Core.Tests`.

## Risks / Trade-offs

- [Nested pagination caps (D3) can truncate very active PRs] -> truncation
  errs toward flagging an update, never suppressing one; revisit if a real PR
  ever exceeds the caps.
- [GraphQL schema drift] -> single query document, integration surface is one
  string; fixtures encode the expected shape so drift fails tests loudly.
- [Accepted me-only hole (D6)] -> documented operational fix (link emails);
  self-limiting and invisible once linked.
- [`reviewed-by:` result growth over time] -> approved/closed PRs drop out of
  `is:open`, and the deriver hides approved ones; the union stays bounded by
  genuinely open review involvement.

## Open Questions

- None blocking. ps-unite fine-grained PAT availability was confirmed in the
  token-creation UI; first real token creation happens at #7's settings flow
  (or manually for local testing).
