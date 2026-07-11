# Proposal: add-state-store

## Why

The skeleton left `PrCenter.Persistence` with an empty `PrCenterDbContext` and a
stub `StateStore` that throws. The polling change (#5) cannot compute the
seen-vs-unread signal without real last-seen markers, and the update deriver
already depends on a marker value being fetched and persisted. This change
implements the marker store and, with it, the persistence foundation --
migrations, startup migration, and a real-SQLite integration-test harness --
that the token-vault (#4) and settings (#7) changes build their own schema on.
Scope is deliberately narrow: only the marker table, whose shape is fully
understood, is designed here (see the roadmap reframe); token/security and
settings tables ride with the changes that decide their columns.

## What Changes

- **`LastSeenMarker` entity + EF model**: keyed by the pull request's stable id,
  carrying the last-seen instant. Never deleted; `SetLastSeen` is an upsert.
- **Real `IStateStore`**: `GetLastSeenAsync` returns the stored instant or null;
  `SetLastSeenAsync` inserts or updates the marker for a pull request id.
- **First EF Core migration** for the marker table, with migrations enabled on
  `PrCenterDbContext`.
- **Startup migration application**: the host applies pending migrations on
  start, before unlock -- the schema is not secret, so it runs while the app is
  still locked; only decrypted data access waits for #4's unlock.
- **SQLite connection defaults** (WAL journal mode and a busy timeout) so the
  Blazor click-through write (#5 MarkSeen) and the background poll write do not
  collide on SQLite's single-writer lock, plus a **5-second command timeout** so
  a query that cannot acquire its lock fails fast rather than hanging the caller.
- **Environment-gated EF diagnostics**: `EnableSensitiveDataLogging` (and
  detailed errors) are turned on **only in the Development environment** so
  parameter values are visible while debugging locally, and never in a
  production/container run where that would leak data into the logs.
- **Real-SQLite integration-test harness**: a temp database file per test,
  migrated and disposed, exercising the real provider (no Testcontainers, no
  in-memory fake). Reused by #4 and #7.
- **Issue #6 (state-store part)**: delete the `StateStoreTests` stub
  `NotImplementedException` tests; add null/whitespace guards on the real
  members with guard tests. The `TokenVault` stub and its tests stay for #4.

## Non-goals

- **No token, security, or settings schema.** `TokenRecord` / `AppSecurity`
  tables are #4's (designed where the KDF and crypto format are decided); the
  `Settings` table is #7's. `TokenVault` remains a throwing stub here.
- **No crypto, unlock, or app-lock behavior** -- all #4.
- **No polling loop, `RefreshQueue`, or `MarkSeen` orchestration** -- #5 wires
  this store to the derivers and the adapter.
- **No UI and no settings entry** -- #7.
- **No marker cleanup or GC** -- the state doc keeps markers forever in v1.
- **No database resilience pipeline / retrying execution strategy.** Unlike the
  HTTP adapter, a local SQLite file's only transient failure is lock contention
  (`SQLITE_BUSY`), which WAL plus the busy timeout already retry internally; the
  5-second command timeout is the fail-fast ceiling, and the poll loop (#5)
  self-heals a failed marker write on the next tick. EF Core's
  `EnableRetryOnFailure` is SQL-Server-only in any case. A custom
  retry-on-busy execution strategy is deliberately not built.

## Capabilities

### New Capabilities

- `state-store`: the `LastSeenMarker` schema and `IStateStore` behavior (get,
  upsert, never-delete), the migration and startup-migration model, the SQLite
  connection defaults, and the real-SQLite integration-test approach that later
  persistence changes reuse.

### Modified Capabilities

*None -- the skeleton's `solution-structure` spec is unaffected; this change
adds behavior behind the existing `IStateStore` seam.*

## Impact

- `PrCenter.Persistence` gains the entity, the migration, the real `StateStore`,
  and the connection configuration; `PrCenter.Persistence.Tests` gains the
  real-SQLite harness and marker tests.
- `PrCenter.Web` composition root gains startup migration application.
- `add-token-vault-and-lock` (#4) and `add-settings-and-onboarding` (#7) build
  their tables and migrations on this foundation; `add-polling-and-refresh` (#5)
  consumes the real `IStateStore`.
- Closes the state-store half of issue #6; the Persistence token half stays
  open for #4.
