# PR-Center Change Roadmap

Planned OpenSpec changes after `add-solution-architecture` is implemented and
archived. Order reflects dependencies, not strict calendar sequence; adjacent
changes without a dependency edge can swap. Names are indicative -- final
scoping happens in each change's own proposal. Source of truth for behavior
remains [pr-center-idea.md](./pr-center-idea.md) and
[pr-center-state.md](./pr-center-state.md).

## Sequence

| # | Change | Depends on | Delivers |
|---|--------|------------|----------|
| 1 | `add-queue-derivation` | skeleton | Core derivation logic (pure, no I/O) |
| 2 | `add-github-adapter` | skeleton | Real `IGitHubFacts` against the GitHub API |
| 3 | `add-state-store` | skeleton | Marker store + persistence foundation (migrations, test harness) |
| 4 | `add-token-vault-and-lock` | 3 | Encryption at rest, unlock flow, app lock gating |
| 5 | `add-polling-and-refresh` | 1, 2, 3, 4 | Poll loop, RefreshQueue, MarkSeen live fetch |
| 5b | `add-queue-enrichment` | 5 | Row data the UI needs: author fact, enriched `QueueItem`, stale carry-over |
| 5c | `replace-marker-with-review-baseline` | 5b | Update baseline = my last review; remove last-seen marker + `MarkSeen` |
| 6 | `add-review-queue-ui` | 5c | The review inbox list itself (presentation-only) |
| 7 | `add-settings-and-onboarding` | 4 | PAT entry, owner list, poll interval UI |
| 8 | `add-containerization` | 6, 7 | Podman/Docker image + volume |
| 9 | `add-observability` | 8 | OpenTelemetry wiring |

## Change summaries

### 1. add-queue-derivation

Membership deriver, update detector, and already-covered flag as pure Core
classes per the state doc: membership recomputed each poll from GitHub facts
(requested? prior non-approved review by me? draft? closed?), update = other
people's commits/comments/reviews since the last-seen marker, covered = at
least one other submitted review. Draft exclusion and the me-only invariants
live here. Heaviest TDD payoff in the project; no I/O, so it can proceed in
parallel with 2 and 3.

### 2. add-github-adapter

`IGitHubFacts` implemented against the GitHub API (transport settled by the
[2026-07-10 spike](./spikes/2026-07-10-github-adapter-spike.md): GraphQL via
hand-rolled HttpClient): two-query discovery per owner (`review-requested:` +
`reviewed-by:`, unioned), nested PR detail in the same query (reviews, commits,
comments, reviewer roster), one fine-grained PAT per owner (resource owner =
that owner), rate-limit handling, per-owner fetch status (ok / error /
misconfigured token -- the spike showed org-owned fine-grained PATs need no
SSO-specific state). Port member set finalized here. Also carries the data
side of the bot/CI decision (actor-type field on the fact records) plus the
matching deriver amendment, and the commit author-identity fallback.

### 3. add-state-store

The last-seen marker store and the reusable persistence foundation the later
data changes build on. Delivers: the `LastSeenMarker` EF entity (keyed by PR
id, upsert on set, never deleted) and the real `IStateStore` implementation;
migrations enabled with the first migration; startup migration application
(the schema is not secret, so it runs while the app is still locked); and the
real-SQLite-file integration-test harness (temp file per test, no
Testcontainers) that #4 and #7 reuse. Token/security and settings schema are
*not* here -- they ride with the changes that design their columns (see #4
and #7), so no table shape is guessed before its behavior is understood. Closes
the state-store part of issue #6 (delete the stub throw tests, add guards +
guard tests).

> Note (2026-07-17): the last-seen marker delivered here is removed by 5c; the
> migrations-and-harness foundation stays (5c, #4, and #7 depend on it).

### 4. add-token-vault-and-lock

App password: KDF choice (Argon2 vs PBKDF2, open question from the skeleton
design), salt + verifier storage, AES encryption of PATs at rest, decrypted
key held in memory, `ITokenVault` implementation, app lock state machine
behavior (Locked on start, Unlock use case, ResetVault wipe path). Polling and
GitHub access gated on unlocked state. Owns its own schema: the token-record
and app-security (salt/verifier + KDF-parameter) tables and their migration,
designed here where the crypto format is decided, on the persistence
foundation from #3.

### 5. add-polling-and-refresh

The polling `BackgroundService` loop (configurable interval, default 5 min),
RefreshQueue orchestration (fetch facts per owner, run derivers, persist
markers/status), manual refresh trigger, and MarkSeen (click-through does a
fresh live fetch of that PR before writing the marker). Wires 1-4 together
end to end; queue state becomes observable through `GetQueue`.

> Note (2026-07-17): `MarkSeen` and the marker-writing poll step are removed by
> 5c, which derives the update baseline from my last review instead.

### 5b. add-queue-enrichment

Core-side enrichment split out of #6 after walking the UX mockups
(docs/pr-center-ux-mockups.html) against the code: the mockup rows need data
`QueueItem` does not carry, and deriving it in Blazor would leak derivation
rules (bot filtering, me-relativity) into the UI. Delivers: the PR author as a
fact (already fetched by the GraphQL query, dropped by the mapper); `QueueItem`
grown with author, when-I-last-looked (the marker instant), when-I-last-reviewed
(latest review by me), the reviewer roster with per-reviewer states
(pending/approved/changes-requested/commented, bot flag, is-me), and the
covering reviewers' names behind the covered flag; and per-owner stale
carry-over in RefreshQueue -- a failed owner's items carry forward from the
previous snapshot with a last-fresh instant on `OwnerStatus`, instead of
silently vanishing (the mockup's "stale 13:55" chip; today's behavior drops
them, the silent-empty trap the idea doc warns about).

### 5c. replace-marker-with-review-baseline

Revises the mark-as-seen model (idea doc, 2026-07-17). The update baseline flips
from the stored last-seen marker to my own latest review instant -- already a
GitHub fact -- so the indicator clears when I review on GitHub, never on a
click-through (which mis-fired: open-and-close cleared it silently). Removes
`MarkSeen`, the `LastSeenMarker` table (a drop migration), and the last-seen
part of `state-store`; the migrations-and-harness foundation stays under
`token-vault`. A never-reviewed PR is New, not Updated (no badge). Lands before
#6 so the UI is built once on the final model.

### 6. add-review-queue-ui

Blazor components for the inbox, presentation-only on top of 5b and 5c: grouped by org
then repo, unseen-first then most-recently-updated within a group (intra-group
sort confirmed here per the idea doc), update badge, last-updated by/when, when
I last reviewed, full reviewer roster with states, already-covered
decoration naming the covering reviewers, click-through opening GitHub (a plain
anchor -- no marking seen; the update indicator is review-derived per 5c),
per-owner status chips with stale labeling, global error banner,
"all caught up" empty state (owner chips stay visible so "no PRs" is provably
different from a failed fetch), and the unlock screen (mockup screen 01; the
password *setup* flow stays in #7). Open questions from the mockup walkthrough
are resolved in the change's design.md: UI freshness = a snapshot-changed event
on the holder (not a timer; SignalR is the render transport); byline collapses
to who-and-when ("pushed 2 commits" / "replied to my comment" need facts that do
not exist). The click-through marker-write question is moot -- 5c removes the
marker entirely.

### 7. add-settings-and-onboarding

Settings page: app password setup, PAT entry per owner, owner list management,
poll interval, reset/wipe path with its no-recovery warning. First-run
experience (locked, no tokens yet). Owns the settings schema -- the
owner-list and poll-interval rows and their migration -- on the persistence
foundation from #3, designed here where the settings shape is understood.

### 8. add-containerization

Dockerfile, Podman/Docker compose, SQLite volume mount, localhost binding,
container start = Locked state verified end to end.

### 9. add-observability

OpenTelemetry traces/metrics/logs wired in the host across poll cycles,
GitHub calls, and persistence.

## Deliberately unscheduled

- Covered-flag sort tiebreaker (idea doc defers it)
- Team-routed review requests (direct-only decision; revisit if teams appear)
- Marker cleanup/GC (explicitly none in v1)
- Any PR mutation or in-app AI review (non-goals)
