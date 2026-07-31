# github-adapter Specification

## Purpose
TBD - created by archiving change add-github-adapter. Update Purpose after archive.
## Requirements
### Requirement: The port exposes owner-queue, single-PR, and login members

`IGitHubFacts` SHALL expose exactly three members: the existing
`GetAuthenticatedUserLoginAsync(owner)`; `GetReviewQueueFactsAsync(owner,
myLogin)` returning an `OwnerFactsResult` (per-owner fetch status plus the
pull-request facts for every open PR relevant to the user in that owner); and
`GetPullRequestFactsAsync(owner, repository, number)` returning the fresh
facts for one pull request, or null when it is inaccessible or does not
exist. A closed or merged pull request SHALL still return facts (with the
closed-or-merged indicator set) so a caller holding its identity can observe
that it is no longer open. All string parameters SHALL be guarded against null
or whitespace.

`OwnerFactsResult` SHALL additionally carry fetch diagnostics on a successful
fetch: the node count returned by each of the two discovery searches before they
are unioned, and the rate-limit reading for the request. These are the only
facts about a fetch that exist solely inside the adapter and are otherwise
unobservable to the caller that records them.

The fetch diagnostics SHALL NOT carry the post-union count: that is already the
length of the returned facts, and duplicating it would create a second source of
truth for one number.

The fetch diagnostics SHALL be absent on every failure path, so a caller can
distinguish a search that returned nothing from a search that never ran.

#### Scenario: Owner queue returns facts plus status

- **WHEN** `GetReviewQueueFactsAsync` succeeds for an owner
- **THEN** the result carries status `Ok` and one `PullRequestFacts` per
  discovered pull request (possibly zero -- an empty queue is still `Ok`)

#### Scenario: A successful fetch carries per-search counts

- **WHEN** `GetReviewQueueFactsAsync` succeeds
- **THEN** the result carries the node count of the review-requested search and
  the node count of the reviewed-by search, each counted before deduplication

#### Scenario: A failed fetch carries no diagnostics

- **WHEN** `GetReviewQueueFactsAsync` returns any non-`Ok` status
- **THEN** the result carries no fetch diagnostics, so the counts read as absent
  rather than as zero

#### Scenario: Single-PR fetch of a merged pull request

- **WHEN** `GetPullRequestFactsAsync` targets a pull request that was merged
- **THEN** facts are returned with the closed-or-merged indicator true, not
  null

#### Scenario: Guarded arguments

- **WHEN** any member is called with a null or whitespace string argument
- **THEN** it throws `ArgumentException` or `ArgumentNullException` before
  any network activity

### Requirement: Discovery unions review-requested and reviewed-by searches

Owner-queue discovery SHALL issue both search qualifiers --
`review-requested:{myLogin}` and `reviewed-by:{myLogin}`, each scoped to open
pull requests in the owner -- and union the results, deduplicated by pull
request id. A requested-only search misses every awaiting-re-review candidate
(the user reviewed, GitHub cleared the request), so both sets are required
for correct membership derivation downstream.

The adapter SHALL count the nodes each qualifier returned before deduplication
and report both counts in the result's fetch diagnostics, so a caller can tell
which search contributed a pull request and how much the union removed.

#### Scenario: Re-review candidate is discovered

- **WHEN** the user has a non-approved review on an open PR and is no longer
  a requested reviewer
- **THEN** the PR appears in the owner-queue facts (found via `reviewed-by:`)

#### Scenario: Overlapping results are deduplicated

- **WHEN** a PR matches both search qualifiers (e.g. re-requested after a
  review)
- **THEN** exactly one `PullRequestFacts` is produced for it

#### Scenario: Overlap is visible in the counts

- **WHEN** a pull request matches both search qualifiers
- **THEN** it is counted in both per-search counts, while the returned facts
  contain it once

### Requirement: Payload mapping follows the verified rules

The adapter SHALL map GraphQL payloads onto the facts model as follows:

- Review and comment actor type: `IsBot` is true iff the author's
  `__typename` is `Bot`; login text SHALL never be used for bot detection.
- Dismissed reviews (state `DISMISSED`) SHALL be omitted from the facts
  entirely.
- Commit land-date maps from `committedDate`.
- Commit author identity maps from the linked user's login when present,
  otherwise the commit author email, otherwise the commit author name.
- Draft pull requests SHALL be fetched and mapped with `IsDraft` true, not
  excluded by the search query -- exclusion is Core's decision.
- The pull-request author login maps from the already-fetched top-level
  `author { login }`; when the author is null or the login blank (a deleted
  "ghost" account), it SHALL fall back to `"unknown"`, matching the existing
  last-updated-by fallback, never a null or blank value. The GraphQL query is
  unchanged.

#### Scenario: Bot review is marked

- **WHEN** a review's author has `__typename: "Bot"` (e.g. login `Copilot`
  or `qodo-code-review`, which vary by surface)
- **THEN** its `ReviewFact.IsBot` is true

#### Scenario: Dismissed review is omitted

- **WHEN** the payload contains a review with state `DISMISSED`
- **THEN** no `ReviewFact` is produced for it

#### Scenario: Unlinked commit author falls back

- **WHEN** a commit's `author.user` is null (email not linked to a GitHub
  account)
- **THEN** `CommitFact.AuthorLogin` carries the commit author email (or name
  when the email is absent), never a null or blank value

#### Scenario: Pull-request author is mapped

- **WHEN** a pull-request payload carries `author { login }`
- **THEN** the mapped identity's author login is that login

#### Scenario: Ghost author falls back

- **WHEN** a pull-request payload's `author` is null (deleted account)
- **THEN** the mapped identity's author login is `"unknown"`, never null or
  blank

### Requirement: Per-owner authentication via the token vault

The adapter SHALL obtain each owner's fine-grained PAT from the Core
`ITokenVault` port per request and send it as a bearer token with a
`User-Agent` header. Tokens SHALL never be cached inside the adapter, logged,
or included in error details.

#### Scenario: Token comes from the vault

- **WHEN** any port member executes for an owner
- **THEN** the request authenticates with the token the vault returned for
  exactly that owner

### Requirement: Fetch failures are per-owner status, not exceptions

`GetReviewQueueFactsAsync` SHALL NOT throw for fetch failures; it SHALL
return a status instead, so one broken owner cannot abort a poll covering
other owners: `MisconfiguredToken` for authentication/authorization failures
(401 or `FORBIDDEN`-type GraphQL errors -- including a PAT created with the
wrong resource owner), and `Error` with a human-readable detail for
rate-limit exhaustion, network failures, server errors, and malformed
payloads. There SHALL be no SSO-specific status.

#### Scenario: Bad token maps to MisconfiguredToken

- **WHEN** GitHub rejects the owner's token (401 or FORBIDDEN-type errors)
- **THEN** the result status is `MisconfiguredToken` with an empty facts list
  and no exception escapes

#### Scenario: Rate-limit exhaustion maps to Error

- **WHEN** the rate limit is exhausted for an owner
- **THEN** the result status is `Error` with a detail describing the
  rate-limit condition

#### Scenario: One owner failing does not poison another

- **WHEN** owner A's fetch fails and owner B's succeeds in the same poll
- **THEN** A yields a failure status and B yields `Ok` facts, independently

### Requirement: The review-queue query requests the rate limit from GitHub

The review-queue query document SHALL request GitHub's `rateLimit` field --
remaining, reset instant, and the cost of the query -- and the adapter SHALL map
it into the result's fetch diagnostics.

The rate limit SHALL be read from the response body rather than from the
`x-ratelimit-*` response headers. The GraphQL API bills in points rather than
requests, so a header count does not describe what the query actually consumed,
and the cost field is what predicts when the limit will be reached.

A response that omits or malforms the rate-limit field SHALL yield an absent
rate-limit reading and SHALL NOT fail the fetch: the rate limit is diagnostic,
and losing it must not cost the user their queue.

#### Scenario: Rate limit is reported with a successful fetch

- **WHEN** `GetReviewQueueFactsAsync` succeeds and the response carries a rate-limit field
- **THEN** the result's fetch diagnostics carry the remaining allowance, the reset instant, and the query cost

#### Scenario: A missing rate limit does not fail the fetch

- **WHEN** a successful response omits the rate-limit field
- **THEN** the status is still `Ok`, the facts are still returned, and the rate-limit reading is absent

