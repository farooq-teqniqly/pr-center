# Proposal: replace-marker-with-review-baseline

## Why

The click-through mark-as-seen model is unreliable in the failure direction that
matters. Opening a PR's link and closing the tab without reading writes the
last-seen marker anyway, clearing the update indicator for changes the user
never saw -- a silent miss, the exact failure this tool exists to prevent.

Keying "seen" off the user's own latest review instead can only over-notify (a
PR glanced at but not yet re-reviewed keeps its badge -- correct, it still awaits
review), which is the safe direction. The review timestamp is already a GitHub
fact fetched every poll, so the update baseline becomes pure derivation and the
stored last-seen marker -- the one piece of app state that was not a pure
projection of GitHub -- is removed entirely. The idea doc's mark-as-seen and
"has an update" decisions were revised on 2026-07-17 to this model; this change
implements that revision.

## What Changes

- **Update baseline = my latest review, not a stored marker.** `UpdateDetector`
  takes the user's last-review instant (derived from the review facts already in
  hand) in place of the last-seen marker. Activity by others strictly after my
  last review flips the indicator; my own activity is the baseline, not an
  update.
- **Never-reviewed PRs are not "updated" -- BREAKING semantic flip.** With no
  review to measure against, an awaiting-first-review PR shows without an update
  badge. Today a null marker yields has-update = true; it now yields false.
- **`MarkSeen` removed.** Click-through opens the PR on GitHub and does nothing
  else; the use case and its click-time live fetch are deleted.
- **Last-seen marker store removed.** `IStateStore` last-seen members, the
  `LastSeenMarker` entity, `StateStore`, and its table go away; a drop migration
  removes the table. The persistence foundation (migrations, integration-test
  harness) stays -- `token-vault` still depends on it.
- **`MyEngagement` loses `LastLookedAt`.** Only `LastReviewedAt` survives on the
  queue item; the "when I last looked" row is dropped.
- **`RefreshQueue` no longer reads or writes markers** -- it derives the update
  baseline from each PR's facts.
- **Docs swept:** `pr-center-state.md` section 2 rewritten to the review-derived
  machine; `pr-center-roadmap.md` #3/#5 scope corrected and this change inserted
  ahead of #6.

## Capabilities

### Modified Capabilities

- `queue-derivation`: `UpdateDetector` measures against the last-review instant;
  a null baseline (never reviewed) yields no update; `QueueItem`/`MyEngagement`
  drop the last-looked instant.
- `polling-and-refresh`: the poll derives the update baseline from facts; the
  mark-as-seen live-fetch requirement is removed.

### Removed Capabilities

- `state-store`: the last-seen marker requirements are removed and the table
  dropped. The migrations-and-harness foundation is retained under
  `token-vault`'s ownership (it already carries its own schema); no marker
  persistence remains.

## Non-goals

- Any change to membership, the already-covered flag, roster derivation, or
  bot-detection rules -- untouched.
- The review-queue UI (#6, branch `add-review-queue-ui-proposal`) -- its
  design D3 (mark-seen) and "I looked" column become obsolete and are corrected
  when that change resumes, not here.
- Backfill or history of past looks -- there was never durable value in the
  marker beyond the update baseline it now derives.

## Impact

- `PrCenter.Core`: `UpdateDetector` (signature/semantics), `QueueItemDeriver`
  (pass last-review instant, drop last-looked), `MyEngagement` (drop
  `LastLookedAt`), delete `MarkSeen`, `IStateStore` (drop last-seen members).
- `PrCenter.Persistence`: delete `LastSeenMarker`, `StateStore`,
  `StateStore.Logging`; add a drop migration for the marker table.
- `PrCenter.Web`: remove `MarkSeen` DI wiring; click-through is a plain anchor.
- Tests: `PrCenter.Core.Tests` (update detector null-baseline flip, deriver),
  `PrCenter.Persistence.Tests` (remove state-store tests).
- Migration: one down/drop migration for the marker table; tokens and settings
  schema untouched.
