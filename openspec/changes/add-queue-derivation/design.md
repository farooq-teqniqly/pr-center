# Design: add-queue-derivation

## Context

The architecture ([pr-center-architecture.md](../../../docs/pr-center-architecture.md))
places the derivers inside `PrCenter.Core` as pure functions, fed by
`IGitHubFacts` and consumed by `RefreshQueue`. The behavior they encode is
specified by the three machines in
[pr-center-state.md](../../../docs/pr-center-state.md); where this design and
that doc disagree, the state doc (and the idea doc's Key Decisions) win.

This change owns the **fact model** because it is the return shape of a Core
port -- Core-owned contract surface, not a private deriver detail (the diagram
puts `IGitHubFacts` in Core). Defining it here lets `add-github-adapter` (#2)
and `add-polling-and-refresh` (#5) bind to a stable seam and proceed in
parallel.

## Goals / Non-Goals

**Goals:**

- A transport-neutral `PullRequestFacts` shape rich enough for all three
  derivations and nothing more.
- Three pure, TDD-covered derivers whose only inputs are facts, the user's
  login, and (for updates) a last-seen marker value.
- Every state-doc invariant expressed as an executable scenario.

**Non-Goals:**

- No I/O, orchestration, sorting, or grouping (see proposal non-goals).
- No display-only fields on `QueueItem` beyond what sorting/decoration in #6
  will need; those are added when #6 specifies them.

## Decisions

### D1: Derivers take `myLogin` as a plain string, not a port

"Relative to the user" needs the authenticated login. That value comes from
`IGitHubFacts.GetAuthenticatedUserLoginAsync` (I/O, per owner), but the
derivers stay pure by taking the resolved login as a parameter. `RefreshQueue`
(#5) resolves it once per owner and passes it in.

### D2: Membership is a single "latest review verdict" rule

After early-out (`IsDraft` -> Excluded, `IsClosed` -> Dropped; both terminal
for the round), membership is a pure function of two facts: am I a *direct*
requested reviewer, and what is my *latest* review verdict.

| | myLatest = null | myLatest = Commented / ChangesRequested | myLatest = Approved |
|---|---|---|---|
| **amRequested** | AwaitingFirstReview | AwaitingReReview | AwaitingFirstReview (author re-requested) |
| **!amRequested** | Untracked (hide) | AwaitingReReview | Approved (hide) |

The single lever is "is my latest review non-approved?" -> `AwaitingReReview`
in both rows. `myLatest` is the user's review with the greatest `SubmittedAt`.

**Same-timestamp tie-break (resolves the review's tie Question).** GitHub review
timestamps are second-granularity, so two of the user's reviews can share the
greatest `SubmittedAt` with different verdicts. The tie breaks toward the most
actionable verdict -- commented, then changes-requested, then approved -- so a
tie keeps the PR shown rather than dropping it. Rationale: on a genuine tie the
"later" review is undefined, and erring toward shown is safe -- a wrongly-shown
PR costs a glance, a wrongly-hidden one is a missed re-review. The intra-
non-approved order (commented before changes-requested) is behaviorally invisible
today (both -> `AwaitingReReview`, not carried on `QueueItem`), so the meaningful
guarantee is only "non-approved beats approved on an equal timestamp." Implemented
as a `ThenBy` secondary sort so it applies to ties only and never overrides a
strictly-later review.

This satisfies the state doc's derived-not-remembered requirement: a
draft-marked-ready PR lands in `AwaitingReReview` iff a prior non-approved
review by the user exists, else `AwaitingFirstReview`, with no special-casing.

### D3: Update detection compares event timestamps to the marker

`UpdateDetector` receives the facts and the last-seen marker
(`DateTimeOffset?` from `IStateStore`). It returns has-update when **any**
update-worthy event (commit, comment, or review) has `AuthorLogin != myLogin`
and `Timestamp > marker`. A `null` marker (never looked) is has-update -- a PR
enters the list unseen. The user's own events and bare `updatedAt` bumps
(labels/title/base) are never events in the fact model, so they cannot flip the
flag.

### D4: Covered flag is other-reviewer submitted reviews only

`CoveredFlag` is true when at least one review has `ReviewerLogin != myLogin`.
Pending review *requests* are in the request roster, not the reviews list, so
they cannot make a PR covered.

### D5: Commit events carry a land-date, not the author date

The update-worthy commit event timestamp is the time the commit *landed* on the
branch (committer/push date), not the author date. A rebased or cherry-picked
commit keeps an old author date; using it would either mis-signal or fail to
signal a real push. The fact field is named to make this explicit
(e.g. `LandedAt`). The GitHub adapter (#2) chooses the concrete API field to
map here.

### D6: `QueueItem` is lean -- identity plus the derived trio

`QueueItem` carries PR identity (the stable id used as the marker key, plus
owner/repo/number/title/url and the last-updated author+instant for display)
and the three derived outputs: `MembershipState`
(`AwaitingFirstReview | AwaitingReReview`), `HasUpdate`, `IsAlreadyCovered`.
Reviewer-roster display, ordering, and grouping are added by #6, not here.
Hidden PRs are not `QueueItem`s at all -- the membership deriver's non-shown
results are a separate outcome the caller filters on.

### D7: Facts are `record`s; equality is the generated default

The fact types are `record`s, so they get compiler-generated value equality for
free. For the leaf records (all primitive/string/enum/`DateTimeOffset` members)
this is correct value equality. For the two collection-holders
(`PullRequestActivity`, `PullRequestFacts`) the generated `Equals` compares the
`IReadOnlyList<>` members by reference, not element-wise, so two instances with
equal contents but distinct list instances compare unequal.

This is fine as built: the facts are a per-poll transport snapshot and no code
compares them for equality -- the derivers read fields, and markers key off
`Identity.Id`, not the whole graph. So we take the default rather than hand-write
`Equals`/`GetHashCode` (or a `sealed class`) for a capability nothing uses. If a
consumer ever needs value equality over a facts graph (dedup, value cache,
`Assert.Equal` on whole facts), give the collection-holders a `SequenceEqual`-based
`Equals` with matching `GetHashCode` at that point.

## Risks / Trade-offs

- **[Membership rule rests on an inferred GitHub semantic]** D2's top-right cell
  (re-requested-after-approval -> AwaitingFirstReview) assumes submitting a
  review clears the user from the requested-reviewers roster, so a present
  request always means a *fresh* ask. This is inferred, not yet verified against
  the API. Recorded as Open Question 1; `add-github-adapter` (#2) must confirm
  it against real payloads before this hardens. If false, the rule needs a
  "request newer than my latest review" timestamp comparison instead.
- **[Fact model over/under-fit]** Defining the shape before the adapter exists
  risks missing a field or carrying a dead one. Mitigation: derive the shape
  strictly from what the three machines read (nothing speculative); #2 may
  request additive fields via a follow-up, but the derivation-facing shape is
  frozen here.

## Open Questions

1. **Re-request clears the review request?** (load-bearing for D2) -- verify in
   #2 against real GitHub payloads. Default assumption: yes.
2. **Approve-then-comment re-shows the PR.** If the user approves, then later
   submits a `Commented` review, `myLatest` is non-approved -> the PR re-enters
   as `AwaitingReReview`. Default: accept (rare; a late comment plausibly
   signals a new concern). Revisit if noisy in practice.
3. **Dismissed reviews.** Do they count toward covered, or toward the user's
   own verdict? Default: do not count -- treat a dismissed review as withdrawn.
   The adapter may simply omit dismissed reviews from the facts, or the model
   carries the state and the derivers exclude it; decide when #2 fixes the
   review-state set.
4. **Bot / CI reviewers.** Should a non-human reviewer (e.g. Copilot, a CI
   bot) count toward "already covered"? Default: humans only -- confirm against
   the idea doc's intent for the covered decoration before #6 renders it.

## Migration Plan

Additive; new pure types and classes in `PrCenter.Core`. Nothing to migrate.
Rollback = drop the types (no consumers until #2/#5 land).
