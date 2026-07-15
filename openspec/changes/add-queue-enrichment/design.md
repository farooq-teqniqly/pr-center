# Design: add-queue-enrichment

## Context

The UX mockups (docs/pr-center-ux-mockups.html) need row data the published
snapshot does not carry. Verified against the code (not inferred):

- `GitHubGraphQlQueries` already fetches the PR `author { login }`;
  `PullRequestFactsMapper` uses it only as the last-updated-by fallback and
  never surfaces it as a fact.
- `QueueItemDeriver.Derive` receives the last-seen instant but discards it
  after computing `HasUpdate`; `QueueItem` carries neither it nor the user's
  latest review time, roster, or covering reviewer names -- all derivable from
  `PullRequestActivity` facts already in hand.
- `RefreshQueue.RefreshOwnerAsync` adds only an `OwnerStatus` for a failed
  owner: its items are absent from the newly published snapshot, so a broken
  token silently empties that owner's rows -- the mockup instead shows them
  labeled "stale 13:55".

## Goals / Non-Goals

**Goals:**

- Every datum on a mockup inbox row is available from `QueueSnapshot` without
  the UI touching facts, markers, or derivation rules.
- A failed owner's previously fetched items stay in the snapshot, labeled with
  when they were last fresh.

**Non-Goals:**

- Blazor UI, sorting, grouping, roster display order -- presentation (#6).
- Byline activity summaries and reply-target facts (#6 open questions).
- Any schema or persistence change.

## Decisions

### D1. Author lives on `PullRequestIdentity`

The author is immutable display-and-identity data ("who opened it"), matching
the record's documented purpose ("how to display and link to it"), unlike
`PullRequestStatus` (mutable state). This takes the constructor from 6 to
exactly 7 parameters -- at the S107 limit, so any future identity field forces
sub-grouping; accepted. Mapper falls back to `"unknown"` when GitHub returns a
null author (deleted "ghost" user), consistent with the existing
last-updated-by fallback. The GraphQL query is unchanged.

### D2. `QueueItem` grows via sub-records, not a flat list

Flat, the constructor would reach 11 parameters. Grouped into genuine domain
concepts (each null-guarded, each within the limit):

- `LastUpdate(By, At)` -- the existing two loose fields, grouped.
- `MyEngagement(LastLookedAt, LastReviewedAt)` -- both nullable; "never" is a
  real state the UI renders ("never" / "--" in the mockup).
- `IReadOnlyList<ReviewerRosterEntry>` -- see D3.
- `IReadOnlyList<string> CoveredBy` -- see D4.

Resulting constructor: identity, lastUpdate, state, hasUpdate, roster,
myEngagement, coveredBy = 7. Author rides for free on `Identity` -- no new
`QueueItem` field.

`LastReviewedAt` is the latest review I submitted regardless of its state,
dismissed included -- "when I last reviewed" is a fact about my activity, not
about whether that review still stands. Reuses the deriver's existing
latest-review-by-me mechanics.

### D3. Roster: union of requested reviewers and submitted reviews

`ReviewerRosterEntry(Login, State, IsBot, IsMe)` with a roster-specific state
enum: `Pending | Approved | ChangesRequested | Commented`.

- Requested-but-not-reviewed logins enter as `Pending` (`IsBot` false --
  requested reviewers arrive as plain logins in the facts; a bot in that list
  cannot be detected, and the mockup only styles bot chips that reviewed).
- A reviewer with submitted reviews takes the state of their latest review.
  Dismissed reviews never reach Core: the github-adapter spec has the mapper
  omit `DISMISSED` reviews from the facts entirely, so "latest review in the
  facts" already means "latest standing review" -- no dismissed filtering in
  the roster derivation. A reviewer whose only reviews were dismissed appears
  as `Pending` if still requested, otherwise not at all, as a consequence of
  the same omission (verified against openspec/specs/github-adapter/spec.md,
  which corrected this design's earlier inference that Core would filter
  dismissed states itself).
- Bots stay in the roster with `IsBot` true (mockup shows qodo/Copilot chips);
  filtering or styling them is presentation.
- `IsMe` uses the existing `GitHubLogin.IsMe` comparison; the UI renders the
  dashed "me" ring from it.
- No ordering imposed -- `QueueItemDeriver` stays ordering-free per its
  contract; the UI sorts.

### D4. `CoveredBy` list replaces the covered bool as source of truth

`CoveredFlag` logic (other humans' reviews; dismissed ones never reach the
facts per the adapter spec) already identifies
the covering reviewers; it returns their distinct logins instead of a bare
bool. `QueueItem.IsAlreadyCovered` remains as a derived property
(`CoveredBy.Count > 0`) so the concept keeps its name. No rule change --
same reviewers, now named.

### D5. Stale carry-over: previous snapshot is the source, null means fresh

On a non-Ok owner outcome (both the status-result path and the catch path),
`RefreshQueue` copies the previous snapshot's items for that owner (matched by
`Identity.Owner`) into the new snapshot instead of dropping them.

`OwnerStatus` gains `DateTimeOffset? LastFreshAt`:

- `Ok` status: `LastFreshAt` is null -- "fresh as of this snapshot"
  (`SnapshotAt` is the timestamp). This avoids injecting a clock into
  `RefreshQueue`; the holder already stamps publication time.
- Non-Ok status: `LastFreshAt` = previous snapshot's `SnapshotAt` when that
  owner was Ok in it, else the previous status's own `LastFreshAt` --
  consecutive failures chain the original fresh instant forward naturally.
- Never-fresh owner (fails on the very first poll): no previous items, null
  `LastFreshAt` with a non-Ok status; the UI shows the error chip with no rows.

An owner removed from the vault is not carried: the loop iterates current
owners only, so its items drop out -- correct, it is no longer polled.

The `VaultLockedException` abort path is untouched: no publish, previous
snapshot survives whole.

### D6. Carried items keep their derived flags as-is

A carried item's `HasUpdate`/`MyEngagement` reflect derivation at its original
fetch. A click-through during the outage writes the marker but the row stays
flagged until the owner recovers -- identical to today's behavior between
polls (the badge already clears on the next poll, not on click), so no special
handling.

## Risks / Trade-offs

- [Identity at exactly 7 constructor parameters] -> next identity field forces
  a sub-record split; acceptable, no candidate field is foreseen.
- [Carried items can contradict markers written during an outage] -> bounded
  by the outage duration; over-notify is the accepted safe direction (same
  rationale as the MarkSeen null-fetch decision in #5).
- [Requested-reviewer bot detection impossible pre-review] -> a bot appears as
  a plain pending human chip until it reviews; cosmetic only, no derivation
  rule (update/covered) depends on a pending entry's bot-ness.
- [Roster/covered records enlarge every snapshot in memory] -> single user,
  tens of PRs; negligible.
- [Breaking shape change to `QueueItem`/`OwnerStatus`] -> consumers are
  in-repo only (RefreshQueue, tests, future UI); no external contract.

## Migration Plan

No persistence or wire migration. Single PR; tests updated alongside. Rollback
= revert.

## Open Questions

- None blocking. Roster display order and bot-chip styling are #6 concerns.
