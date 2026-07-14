## Why

Changes 1-4 delivered the derivers, the GitHub adapter, the marker store, and the
token vault as separate verified pieces, but nothing runs them: there is no poll
loop, no way to observe a derived queue, and no mark-as-seen path. This change
wires them together end to end so the app produces its core artifact -- the
review queue -- on a schedule and on demand.

## What Changes

- **Polling `BackgroundService`** in `PrCenter.Web`: runs `RefreshQueue` on a
  configurable interval (default 5 minutes, from `appsettings.json` until the
  settings UI change owns it), skipping while the app lock is not `Unlocked`.
- **`RefreshQueue` use case** in `PrCenter.Core`: for each owner with a stored
  token, resolve the authenticated login, fetch review-queue facts, run the
  derivers against last-seen markers, and publish an in-memory queue snapshot
  (items plus per-owner fetch status). A per-owner fetch failure degrades that
  owner's status; it never aborts the poll.
- **`GetQueue` use case**: returns the current snapshot (or an explicit
  never-polled state). Sorting and grouping stay in the UI change (#6).
- **`MarkSeen` use case**: on click-through, live-fetch that PR's facts and
  write the last-seen marker from the fetched activity (not wall clock). A null
  fetch (PR inaccessible or gone) writes no marker.
- **Manual refresh trigger**: a single wake mechanism the UI (later) and the
  unlock path both poke; triggers coalesce and polls never overlap. Unlocking
  pokes it so the first poll happens immediately, not up to an interval later.
- **`ITokenVault.ListOwnersAsync`**: the vault enumerates owners with a stored
  token; that enumeration IS the owner list for polling. No settings schema is
  pulled forward from change #7.

## Capabilities

### New Capabilities
- `polling-and-refresh`: the poll loop, refresh orchestration, queue snapshot
  and its observation (`GetQueue`), mark-as-seen via live fetch, manual refresh
  trigger, and lock gating of all of it.

### Modified Capabilities
- `token-vault`: gains a requirement to enumerate the owners that have a stored
  token (`ListOwnersAsync`). Enumeration reads the plaintext owner key column,
  involves no decryption, and works regardless of lock state.

## Impact

- **`PrCenter.Core`**: new use cases (`RefreshQueue`, `GetQueue`, `MarkSeen`),
  a queue-snapshot holder abstraction, and the `ITokenVault` port addition.
- **`PrCenter.Persistence`**: `TokenVault` implements `ListOwnersAsync`.
- **`PrCenter.Web`**: the polling `BackgroundService`, refresh-trigger wiring,
  DI registration, and the poll-interval configuration binding.
- **No schema change**: markers and tokens already exist; no new tables.
- **Depends on** archived changes 1-4 (queue-derivation, github-adapter,
  state-store, token-vault-and-lock); **unblocks** #6 (review-queue UI).

## Non-goals

- Any UI (queue list, unlock screen, refresh button) -- change #6/#7 territory;
  this change is observable through `GetQueue` and tests only.
- Settings schema (owner list, poll interval rows) -- change #7 owns it.
- Sorting/grouping of queue items -- UI-side, change #6.
- Marker cleanup/GC, PR mutation, observability wiring -- explicitly out per
  the roadmap.
