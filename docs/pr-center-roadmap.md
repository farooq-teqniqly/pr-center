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
| 3 | `add-state-store` | skeleton | SQLite schema + real `IStateStore` |
| 4 | `add-token-vault-and-lock` | 3 | Encryption at rest, unlock flow, app lock gating |
| 5 | `add-polling-and-refresh` | 1, 2, 3, 4 | Poll loop, RefreshQueue, MarkSeen live fetch |
| 6 | `add-review-queue-ui` | 5 | The review inbox list itself |
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

EF Core entity model and migrations for last-seen markers (keyed by PR id,
never deleted), owner/settings rows, and encrypted token storage; real
`IStateStore` implementation. Integration tests hit a real SQLite file.

### 4. add-token-vault-and-lock

App password: KDF choice (Argon2 vs PBKDF2, open question from the skeleton
design), salt + verifier storage, AES encryption of PATs at rest, decrypted
key held in memory, `ITokenVault` implementation, app lock state machine
behavior (Locked on start, Unlock use case, ResetVault wipe path). Polling and
GitHub access gated on unlocked state.

### 5. add-polling-and-refresh

The polling `BackgroundService` loop (configurable interval, default 5 min),
RefreshQueue orchestration (fetch facts per owner, run derivers, persist
markers/status), manual refresh trigger, and MarkSeen (click-through does a
fresh live fetch of that PR before writing the marker). Wires 1-4 together
end to end; queue state becomes observable through `GetQueue`.

### 6. add-review-queue-ui

Blazor components for the inbox: grouped by org then repo, unseen-first then
most-recently-updated within a group, update badge, last-updated by/when, when
I last looked/reviewed, full reviewer roster with states, already-covered
decoration, click-through opening GitHub + marking seen, per-owner status
indicators, global error banner, "all caught up" empty state.

### 7. add-settings-and-onboarding

Settings page: app password setup, PAT entry per owner, owner list management,
poll interval, reset/wipe path with its no-recovery warning. First-run
experience (locked, no tokens yet).

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
