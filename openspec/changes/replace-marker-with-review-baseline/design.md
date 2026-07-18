# Design: replace-marker-with-review-baseline

## Context

Verified against the code, not inferred:

- `UpdateDetector.HasUpdate(facts, myLogin, lastSeen)` returns true on a null
  `lastSeen` (never-looked = unseen), else true when any other-human comment/
  review or any commit (bots included) has `When > marker`. Bot comments/reviews
  are filtered; commits are not.
- The `lastSeen` value flows from `IStateStore.GetLastSeenAsync` in
  `RefreshQueue.DeriveItemAsync`, keyed by `Identity.Id`.
- `MarkSeen.MarkSeenAsync` live-fetches one PR and writes
  `IStateStore.SetLastSeenAsync` with the high-water mark of activity.
- `MyEngagement(LastLookedAt?, LastReviewedAt?)` carries both; `LastReviewedAt`
  is already the user's latest review instant, derived in the enrichment change
  from the review facts.
- `state-store` is the only capability whose subject is the marker;
  `token-vault` owns its own token/security schema and only reuses the
  migrations-and-harness foundation `state-store` established.

The key realization: `LastReviewedAt` already computes exactly the baseline the
new model needs. The marker was a second, less reliable source of truth for the
same "since when" question.

## Goals / Non-Goals

**Goals:**

- Update baseline is the user's latest review instant, derived each poll.
- No stored per-PR read state; the app is a pure projection of GitHub.
- `MarkSeen` and the marker table are gone, cleanly, with a drop migration.

**Non-Goals:**

- Membership, covered, roster, bot rules -- untouched.
- The #6 UI rework -- corrected when that change resumes.

## Decisions

### D1. `UpdateDetector` measures against the last-review instant

The third parameter changes from a stored last-seen marker to the user's latest
review instant. The comparison logic is otherwise unchanged: other-human
comments/reviews and all commits with a timestamp strictly after the baseline
produce has-update; own activity and bot comments/reviews do not. The parameter
is renamed to name the concept (`myLastReviewedAt`), not the old storage.

### D2. Null baseline yields no update (the semantic flip)

A user who has never reviewed a PR has no baseline. Today that returns true
(never-looked = unseen); it now returns **false**: a never-reviewed PR is *new*,
not *updated*, and the update badge is meaningful only relative to a review.
This is the one behavioral flip, and it matches the mockup's awaiting-first-
review rows (no badge, e.g. #1307). Membership still lists the PR; only the
badge is suppressed.

Rationale over the alternative (baseline = when I was requested): the request
instant is not currently a fact, and "activity since I was requested" is noise
before I have engaged -- everything on a fresh PR is post-request. Deferred; not
needed.

### D3. `MyEngagement` drops `LastLookedAt`

`LastReviewedAt` becomes the sole engagement instant, and it doubles as the
update baseline. `MyEngagement` could collapse to that single field; keeping it
as a one-field record preserves the domain grouping and leaves room without
forcing a `QueueItem` constructor reshuffle. The deriver passes
`LastReviewedAt` both into the item and into `UpdateDetector`, so the baseline
and the displayed "when I last reviewed" are provably the same instant.

### D4. `RefreshQueue` stops touching the store

`DeriveItemAsync` no longer calls `GetLastSeenAsync`; it derives the last-review
instant from the facts (reusing the enrichment deriver's existing
latest-review-by-me mechanics) and hands it to `UpdateDetector`. The poll no
longer reads or writes any marker. This also drops one DB round-trip per PR per
poll.

### D5. Marker store removed, foundation retained, table dropped

`IStateStore` (last-seen members), `LastSeenMarker`, `StateStore`, and
`StateStore.Logging` are deleted. Because the table already shipped (migration
`20260711180657_InitialCreate`), removal needs a new migration that drops the
marker table -- EF migrations are forward-only; the table cannot be un-shipped
by deleting the entity. The migrations infrastructure, the DbContext, and the
real-SQLite integration-test harness stay: `token-vault`'s schema and tests
depend on them. `state-store` as a *capability* is removed; the *persistence
foundation* it introduced lives on.

If `IStateStore` has no remaining members after the last-seen removal, the port
is deleted outright; if any non-marker member exists, only the last-seen members
go. (Verify at implementation -- current reads show last-seen as its whole
surface, so the port likely goes entirely.)

### D6. Click-through becomes a plain anchor

`MarkSeen` and its DI registration are removed. In the UI (when #6 resumes) the
title is a plain `target="_blank"` anchor to `Identity.Url` with no side
effect. The badge clears on the next poll after the user reviews on GitHub --
never on click.

## Risks / Trade-offs

- [Semantic flip on never-reviewed PRs] -> a first-review PR that had activity
  before I first look no longer shows a badge; acceptable, it is new and listed,
  and the badge regains meaning the moment I review. Covered by an explicit
  spec scenario.
- [Badge persists until I review] -> a PR I read on GitHub but defer reviewing
  keeps its badge across polls. This is the intended over-notify direction, not
  a regression; the PR genuinely still awaits my review.
- [Forward-only migration] -> dropping the table needs a real migration, not an
  entity deletion; miss it and the orphaned table lingers (harmless but untidy).
  Called out as a task.
- [#6 branch now carries obsolete design] -> `add-review-queue-ui` D3 and the
  "I looked" column contradict this model; flagged as that change's fix-up, not
  silently left to rot.

## Migration Plan

One forward migration drops the last-seen marker table; token and security
tables are untouched, so no data migration and no unlock dependency. Code
deletion and spec removal ride the same PR. Rollback = revert the migration and
the code. Sequencing: this change lands before #6 resumes, so the UI is built
once, on the final model.

## Open Questions

- None blocking. Whether `IStateStore` is deleted entirely or trimmed is a
  mechanical implementation detail resolved by reading its final surface (D5).
