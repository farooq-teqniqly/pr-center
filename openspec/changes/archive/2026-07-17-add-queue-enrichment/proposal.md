# Proposal: add-queue-enrichment

## Why

The review-queue UI (roadmap #6) needs row data the current `QueueItem` does not
carry: the UX mockups (docs/pr-center-ux-mockups.html) show PR author, "when I
last looked" / "when I last reviewed" stamps, the full reviewer roster with
per-reviewer states, the covering reviewers' names, and stale-but-visible data
for an owner whose fetch failed. Deriving these in Blazor components would leak
derivation rules (bot filtering, me-relativity) into the UI layer, so the
projection is enriched in Core first and the UI change (#6) becomes
presentation-only.

## What Changes

- **PR author becomes a fact.** The GraphQL query already fetches
  `author { login }`; the mapper drops it. Add an author field to the facts
  model and map it.
- **`QueueItem` grows the row data** (grouped into cohesive sub-records to stay
  within the 7-parameter limit):
  - PR author login.
  - When I last looked (the last-seen marker instant, already available to the
    deriver; null when never looked).
  - When I last reviewed (latest review by me, from existing review facts; null
    when never reviewed).
  - Reviewer roster: each reviewer's login, latest review state (or pending for
    requested-but-not-reviewed), bot flag, and whether the entry is me.
  - Covering reviewer logins (the humans behind the existing
    `IsAlreadyCovered` flag), so the covered decoration can name them.
- **Per-owner stale carry-over** — **BREAKING** (published snapshot behavior):
  today a per-owner fetch failure drops that owner's items from the new
  snapshot; requested reviews silently vanish because a token broke, the exact
  silent-empty trap the idea doc warns about. Instead, a failed owner's items
  are carried forward from the previous snapshot and `OwnerStatus` gains the
  instant the carried data was last successfully fetched, so the UI can label
  it "stale since 13:55".

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `github-facts-model`: pull request facts gain the author's login.
- `github-adapter`: the mapper surfaces the already-fetched PR author into the
  facts model.
- `queue-derivation`: `QueueItem` carries author, last-looked and last-reviewed
  instants, the reviewer roster with states, and covering reviewer names; the
  roster and covered-by derivations follow the existing me-relative and
  bot-detection rules.
- `polling-and-refresh`: a per-owner fetch failure carries the owner's previous
  items forward instead of dropping them; `OwnerStatus` reports when carried
  data was last fresh.

## Non-goals

- Any Blazor UI, sorting, or grouping - that is `add-review-queue-ui` (#6).
- The byline activity summary ("dkellner pushed 2 commits") - deferred to #6 as
  a presentation question; whether it needs more than `LastUpdatedBy/At` is an
  open question recorded in the roadmap.
- Reply-target detection ("replied to my comment") - not derivable from the
  current comment facts; #6 renders plain comment activity.
- Marker GC, settings schema, or any mutation of PR state.

## Impact

- `PrCenter.Core`: `Facts` (author field), `Derivation` (`QueueItem`,
  `QueueItemDeriver`, roster/covered-by derivation), `Queue` (`RefreshQueue`
  carry-over, `OwnerStatus`).
- `PrCenter.GitHub`: `PullRequestFactsMapper` (author mapping; query unchanged).
- Tests: `PrCenter.Core.Tests` (deriver, carry-over), `PrCenter.GitHub.Tests`
  (mapper).
- No persistence schema change: last-seen markers already store everything
  needed.
