## Context

Changes 1-4 delivered the pure derivers (`QueueItemDeriver` and friends), the
GitHub adapter (`IGitHubFacts`), the marker store (`IStateStore`), and the token
vault plus app lock (`ITokenVault`, `IAppLock`). Nothing invokes them together:
there is no poll loop, no observable queue, and no mark-as-seen path. The
architecture doc already places a polling `BackgroundService` in `PrCenter.Web`
and the `RefreshQueue` / `GetQueue` / `MarkSeen` use cases in `PrCenter.Core`.

Grounding facts verified in source before this design:

- `UpdateDetector.HasUpdate` compares `activity.When > marker` against the
  last-seen instant; a null marker means "unseen" (`UpdateDetector.cs`).
- `IStateStore.SetLastSeenAsync(pullRequestId, seenAt)` stores a bare
  `DateTimeOffset`; nothing constrains where that instant comes from.
- `IGitHubFacts` reports per-owner fetch failures through `OwnerFactsResult`
  status, never throws for them; a locked vault is the one exception
  (`VaultLockedException` propagates).
- `OwnerToken.Owner` is a plaintext primary-key column; only the token bytes
  are encrypted. Enumerating owners requires no decryption.
- The persistence adapter registers `PrCenterDbContext`, `IStateStore`,
  `ITokenVault`, and `IAppLock` as scoped; only `VaultKeyHolder` is a
  process singleton (`PersistenceServiceCollectionExtensions.cs`).

## Goals / Non-Goals

**Goals:**

- A poll loop that refreshes the queue on an interval (default 5 minutes) and
  on demand, only while the app lock is `Unlocked`.
- A `RefreshQueue` use case that turns "owners with tokens" into a published
  in-memory queue snapshot (derived items plus per-owner fetch status).
- `GetQueue` exposing that snapshot, including an explicit never-polled state.
- `MarkSeen` doing the click-through live fetch and marker write.
- An unlock that results in an immediate first poll.

**Non-Goals:**

- Any UI, settings schema, sorting/grouping, marker GC, observability wiring
  (see proposal Non-goals).

## Decisions

### D1. Owner list comes from the vault: `ITokenVault.ListOwnersAsync`

The set of owners to poll is exactly the set of owners with a stored token.
`ITokenVault` gains `ListOwnersAsync` returning the stored owner names.

- **Why not appsettings:** a second owner list drifts from the tokens; an owner
  without a token cannot be polled anyway.
- **Why not pull the settings schema forward from change #7:** the roadmap rule
  is that a schema rides with the change that designs its behavior; #7 owns
  owner-list management UI and may still choose the vault as its source.
- **Lock semantics:** enumeration reads the plaintext `Owner` key column and
  involves no decryption, so it does not throw `VaultLockedException`. Gating
  polling on lock state is the app lock's job (D4), not the vault's.

### D2. MarkSeen writes the marker from fetched activity, not wall clock

`MarkSeen(owner, repository, number, pullRequestId)` live-fetches the PR via
`IGitHubFacts.GetPullRequestFactsAsync`, then writes the marker as the **maximum
activity timestamp** in the fetched facts (commits' `LandedAt`, comments'
`CreatedAt`, reviews' `SubmittedAt` -- the same events `UpdateDetector` reads,
unfiltered: including my own and bots', since the marker is a high-water mark of
what existed when I looked, not an update judgment).

- **Why not `DateTimeOffset.UtcNow`:** clock skew between this workstation and
  GitHub either leaves a future-stamped event flagged forever or silently
  swallows activity that landed just after the click. Deriving the marker from
  the same timestamp domain the detector compares against is skew-immune.
- **Null fetch (PR inaccessible or gone):** write no marker. The PR is leaving
  the list anyway; an invented timestamp helps nothing.
- **Empty activity (defensive only -- a PR always has at least one commit):**
  fall back to the facts' last-touch stamp from `PullRequestStatus`. With no
  activity events, `HasUpdate` is false for any marker value, so this choice is
  functionally inert; it just keeps the marker in GitHub's timestamp domain.

### D3. One wake mechanism: a refresh trigger poked by manual refresh and unlock

Core defines a small `IRefreshTrigger` (fire-and-forget `RequestRefresh()`)
backed by a bounded signal (`Channel` capacity 1, drop-write). The polling
`BackgroundService` awaits this one trigger; the interval is realized as another
poker of it (a `TimeProvider` timer whose callback calls `RequestRefresh()`), so
the loop has a single wake source rather than racing a `PeriodicTimer` against
the trigger. Consequences:

- Concurrent pokes coalesce into one poll; polls never overlap because the
  single loop is the only consumer, and it holds no trigger reader while a poll
  is in flight, so wakes arriving mid-poll (timer or manual) collapse into at
  most one follow-up poll.
- A new Core `UnlockApp` use case wraps `IAppLock.UnlockAsync` and pokes the
  trigger on success, so unlocking yields an immediate first poll instead of
  waiting up to an interval. The future unlock UI (#6/#7) calls the use case,
  not `IAppLock` directly.
- A poke while locked wakes the loop, which sees a non-`Unlocked` state and
  goes back to sleep -- harmless, and it keeps the loop the single place that
  consults the lock.

### D4. Lock gating lives in the loop; a mid-poll lock aborts quietly

Each wake, the loop checks `IAppLock.GetStateAsync`; anything but `Unlocked`
skips the poll. A `VaultLockedException` that escapes mid-poll (vault reset
during a poll) is owned by `RefreshQueue`, which catches it, logs a warning
(baseline rule: no silent catch), and abandons the poll without publishing --
leaving the previous snapshot untouched. The loop therefore needs no
lock-specific handling of its own. Per-owner fetch failures are not exceptions
at all -- they arrive as `OwnerFetchStatus` values and degrade only that
owner's entry in the snapshot.

### D5. The queue snapshot is an in-memory process singleton

`RefreshQueue` publishes an immutable `QueueSnapshot` (derived queue items,
per-owner fetch status, snapshot instant) into a singleton holder via atomic
reference swap; `GetQueue` reads it. No facts are persisted -- consistent with
"membership is derived each poll" and the accepted restart behavior (empty list
until unlock plus first poll). The never-polled state is explicit (holder starts
empty), so the UI change can distinguish "no PRs" from "not polled yet". The
snapshot instant comes from `TimeProvider`, keeping the holder testable.

### D6. The Web loop owns DI scoping; use cases stay scope-agnostic

`PrCenterDbContext` and the adapters are scoped, but a `BackgroundService` is a
singleton. The polling service creates a DI scope per wake and resolves the
refresh use case from it through the `IRefreshQueue` abstraction (implemented by
`RefreshQueue`), which also keeps the loop unit-testable with a substitute. Use
cases take their ports via constructor injection and never touch
`IServiceProvider`.

### D7. `myLogin` is resolved per owner per poll, no cross-poll cache

`GetAuthenticatedUserLoginAsync` is one cheap query riding the same poll; caching
across polls risks staleness when a PAT is replaced, for negligible savings at a
5-minute cadence.

## Risks / Trade-offs

- **[Vault reset between `ListOwnersAsync` and token use]** mid-poll
  `VaultLockedException` -> D4: quiet abort, warning log, stale-but-intact
  snapshot.
- **[Marker never written when the live fetch nulls]** the PR would stay
  "unseen" if it somehow re-entered the list -> acceptable: re-entry after
  inaccessibility is rare, and "unseen" is the safe direction (over-notify,
  never under-notify).
- **[Snapshot is process-local]** a restart shows an empty queue until unlock
  plus first poll -> already an accepted consequence in the idea/state docs.
- **[Trigger poke during an in-flight poll coalesces rather than queues]** a
  refresh requested mid-poll runs at most once after the current poll -- the
  drop-write channel keeps exactly one pending wake, so the freshness loss is
  bounded by one poll duration.
- **[appsettings-based interval until #7]** changing the interval requires a
  container restart until the settings UI lands -> acceptable for a
  single-user tool; #7 replaces the source, not the loop.

## Open Questions

- None blocking. The `QueueSnapshot` item shape (which derived fields ride
  along for #6's UI) is finalized in the specs artifact; the deriver output
  (`QueueItem`) already carries membership state, update flag, and covered
  flag, so this is selection, not invention.
