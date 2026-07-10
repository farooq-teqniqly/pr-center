# Spike: GitHub adapter API verification (2026-07-10)

Findings that feed the `add-github-adapter` change (roadmap #2). Method: raw
read-only probes against the live GitHub API using the `gh` CLI's OAuth token
for semantics and a throwaway fine-grained PAT (PerfectServe resource owner,
revoked after the spike) for the token-capability questions. All findings are
verified against real payloads, not documentation.

## Scoreboard

| # | Question | Verdict |
| --- | --- | --- |
| P1 | Does a fine-grained PAT work with GraphQL, including org data? | Yes. Full nested search query against PerfectServe cost 1 rate-limit point |
| P2 | Does submitting a review clear me from `requested_reviewers`? (design OQ1) | Yes. A present request is always a fresh ask; re-request re-adds |
| P3 | Does `reviewed-by:` search find commented-only PRs? | Yes. 7 results vs 4 for `review-requested:` -- 6 PRs invisible to the requested-only query |
| P4 | How do bots appear in payloads? (design OQ4) | REST `user.type == "Bot"`; GraphQL `author.__typename == "Bot"`. Login text is unreliable |
| P5 | Which commit date serves the land-date (design D5)? | `committedDate` (GraphQL) / `commit.committer.date` (REST). No rebase specimen found; fields verified present |
| P6 | What does a dismissed review look like? (design OQ3) | List state becomes `DISMISSED`; the original verdict is not in the list payload. Filtering them out is trivial and lossless |
| P7 | needs-SSO failure shape | Deferred -- see "SSO simplification" below; likely moot for our PAT model |

## Transport decision: GraphQL

One nested GraphQL search query returned discovery plus all per-PR detail
(review requests, reviews, commits with dates and author emails, comments)
for 7 matching PRs at a cost of **1 point** against a 5,000-point/hour budget.
A full poll is ~2 queries per owner (`review-requested:` + `reviewed-by:`)
x 3 owners = ~6 points. The REST equivalent is 2 search calls plus 4-6 detail
calls per PR (~1,800 calls/hour at 30 open PRs on a 5-minute poll) against the
same 5,000/hour budget.

Consequences:

- Hand-rolled `HttpClient` + one GraphQL query document. No client library:
  octokit.net is REST-only, and octokit.graphql.net is a maintenance risk for
  a single query.
- Search rate limit is a separate 30/minute pool; ~6 searches per poll is
  negligible.
- `PullRequest.reviews` supports a server-side `author:` filter argument,
  so "my latest review" needs no client-side paging.

## Discovery requires two queries per owner

`review-requested:{me}` returns only PRs where the request is *currently*
pending. PRs in the `AwaitingReReview` membership state (I reviewed, GitHub
cleared the request) are only found by `reviewed-by:{me}`. Live data:
7 reviewed-by vs 4 review-requested with 1 overlap -- the overlap was a PR
re-requested after my review (P2's specimen). The adapter must union both
result sets, deduplicated by PR id.

Note: `docs/Get-PRQueue.ps1` uses only `--review-requested`, so it misses the
re-review set entirely; its `reviews.state` filter is also dead code (the field
is not in its `--json` list). The adapter supersedes it.

## Review-request semantics (design OQ1 resolved)

- Submitting any review (including commented-only) removes the user from
  `requested_reviewers`. Verified: Voice#140 -- 13 commented reviews by me,
  not in the roster.
- An author re-request re-adds the user. Verified via SystemApi#304 timeline:
  requested Jun 8 -> reviewed Jun 9 -> `review_requested` event again Jul 10.
- Therefore D2's "approved + requested -> AwaitingFirstReview" cell rests on
  verified behavior: a present request always means a fresh ask.

## Bot identification (design OQ4 data)

The only reliable signal is the user/actor *type*:

- REST: `user.type == "Bot"`.
- GraphQL: `__typename == "Bot"` on the author/actor.

Login text varies by surface for the same bot: `qodo-code-review[bot]` (REST
reviews), `qodo-code-review` (GraphQL author), `Copilot` (REST inline
comments -- no `[bot]` suffix at all). Do not sniff login suffixes. The facts
model gains an additive author-type field per the design OQ4 sequencing; the
policy decision (excluded from covered and comment/review updates, bot commits
still count) lives in the idea/state docs.

Real-world validation: SystemApi#314's only human review is dismissed, the
rest are bot reviews -- with the default policy (exclude bots and dismissed),
covered = false there, which matches reviewer intuition.

## Dismissed reviews (design OQ3 resolved)

A dismissed review appears in the reviews list with `state: "DISMISSED"`; the
original verdict (approved / changes-requested) is not present in the list
payload. The adapter omits dismissed reviews from `ReviewFact`s -- consistent
with the "treat as withdrawn" default, and no information the derivers need is
lost.

## Commit author identity is unreliable (new finding)

`commit.author`/`commit.committer` **user objects are null** when the commit
email is not linked to a GitHub account. Observed on the user's own work
commits (`fmahmud@perfectserve.com`, unlinked). Consequences:

- `CommitFact.AuthorLogin` cannot always be a GitHub login. The adapter needs
  a fallback (git author email or name, available in the same query).
- The me-only invariant has a hole: the user's own unlinked-email push to a PR
  they review would not match `myLogin` and would falsely flag `HasUpdate`.
  Mitigation options for #2's design: a configured "my emails" list, or accept
  the rarity. Others' unlinked commits resolve to "someone else," which is the
  correct outcome anyway.

## Noisy reviews list (context)

Every unbatched inline comment submits as its own `COMMENTED` review (13 from
one person on one PR observed). The timestamp-latest rule in `MembershipDeriver`
already tolerates this; adapters and tests should not assume one review per
person per round.

## PAT model validated

- Fine-grained PATs are org-scoped by resource owner: one PAT per owner maps
  1:1 onto GitHub's constraint. The app's per-owner PAT design is confirmed,
  not just assumed.
- PerfectServe allows fine-grained PATs (probe worked); **ps-unite also shows
  up as an available resource owner** (checked in the token-creation UI), so
  all three owners are covered.
- **SSO simplification:** an org-resource-owner fine-grained PAT needs no
  separate SAML authorization -- it is org-scoped by construction (the spike
  PAT worked against a SAML org with no SSO dance). The roadmap's "needs-SSO"
  per-owner status therefore collapses into a generic "unauthorized /
  misconfigured token" state for fine-grained PATs; #2 should not build
  SSO-specific handling.

## Rate-limit budgets observed (fine-grained PAT)

| Pool | Limit |
| --- | --- |
| core (REST) | 5,000/hour |
| GraphQL | 5,000 points/hour |
| search | 30/minute (separate pool) |

## Items intentionally left open

- No rebase specimen found, so the "committedDate updates on rebase" behavior
  is asserted from Git semantics, not observed. Low risk.
- needs-SSO / 403 shapes unprobed (requires an unauthorized token); expected
  moot per the SSO simplification above.
