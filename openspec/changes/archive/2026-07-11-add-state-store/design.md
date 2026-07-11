# Design: add-state-store

## Context

The skeleton (`add-solution-architecture`) left `PrCenterDbContext` empty and
`StateStore` throwing. Marker semantics are fixed by
[pr-center-state.md](../../../docs/pr-center-state.md): a last-seen marker
persists keyed by pull request id, is never proactively deleted, and is the
baseline the update deriver compares against. The architecture
([pr-center-architecture.md](../../../docs/pr-center-architecture.md)) puts the
store behind the Core `IStateStore` port, implemented in `PrCenter.Persistence`
against SQLite. This change also lays the persistence foundation (migrations,
startup migration, connection defaults, test harness) that #4 and #7 reuse; the
roadmap reframe keeps token/security and settings schema out of scope.

## Goals / Non-Goals

**Goals:**

- Real `IStateStore` over a `LastSeenMarker` EF entity, upsert semantics,
  never deleted.
- A migration and a startup-migration model that runs while the app is locked.
- SQLite connection defaults that make concurrent poll/click writes safe and
  fail fast.
- A real-SQLite integration-test harness that #4/#7 reuse.

**Non-Goals:** token/security/settings schema (#4/#7), crypto/unlock (#4),
polling (#5), UI (#7), marker GC, DB resilience pipeline (see D6).

## Decisions

### D1: One entity -- `LastSeenMarker { PullRequestId, SeenAt }`

`PullRequestId` (string) is the primary key -- the same stable id
`PullRequestFacts.Identity.Id` carries, so `#5` writes the marker it read.
`SeenAt` is a `DateTimeOffset`. No other columns; owner/repo are derivable from
facts and not needed to key or read a marker. The entity is configured with the
Fluent API in `OnModelCreating` (not data annotations on a Core type -- the
entity lives in `PrCenter.Persistence`, so Core stays EF-free).

### D2: `SetLastSeenAsync` is an upsert; `GetLastSeenAsync` returns null when absent

`Get` is `FindAsync(id)` -> `SeenAt` or null. `Set` finds the marker and updates
`SeenAt`, or inserts a new row -- a single `SaveChangesAsync`. No delete path
(state doc: markers are never removed). Guards: both members
`ThrowIfNullOrWhiteSpace(pullRequestId)`; `SetLastSeenAsync` has no reference
guard on the `DateTimeOffset`. This closes the state-store part of issue #6
(the stub throw-tests are deleted, guards + guard tests land here).

### D3: `DateTimeOffset` stored as EF's default SQLite TEXT

The SQLite provider maps `DateTimeOffset` to ISO-8601 TEXT with offset.
Round-trip and ordering are stable for the update deriver's `> marker`
comparison because the format is fixed-width and lexicographically ordered for
a common offset. Markers are written and read by this app only, so there is no
cross-writer format risk. No custom value converter.

### D4: Migrations enabled; the host migrates on startup while locked

`PrCenterDbContext` gets an initial migration (`InitialCreate`) for the marker
table. The `PrCenter.Web` host applies pending migrations on startup via
`Database.MigrateAsync()` before the app is unlocked -- the schema is public,
so migration does not need the decrypted key; only reading/writing decrypted
token data waits for #4's unlock. Migration runs against the SQLite file on the
mounted volume.

- *Alternative: `EnsureCreated`.* Rejected -- it bypasses migrations and cannot
  evolve the schema, which #4 and #7 will extend.

### D5: SQLite connection + context configuration

Configured where the context options are built (see D7 for the env seam):

- **WAL journal mode** (`PRAGMA journal_mode=WAL`) so readers never block the
  single writer.
- **Busy timeout** so a writer waits for the lock instead of failing
  immediately under poll-vs-click contention.
- **5-second command timeout** (`CommandTimeout`) as the fail-fast ceiling: on
  SQLite this bounds the lock-acquisition wait (SQLite has no server-side
  execution killer), so a genuinely stuck write errors rather than hanging the
  Blazor circuit or the poll.

### D6: No DB resilience pipeline -- busy timeout is the equivalent

Unlike the HTTP adapter's standard resilience handler, the state store gets no
retrying execution strategy. SQLite's only transient failure is `SQLITE_BUSY`
(lock contention), which WAL + the busy timeout already retry internally; the
command timeout is the ceiling; and `#5`'s poll loop self-heals a failed marker
write on the next tick (a failed MarkSeen just leaves the PR flagged until the
next click). `EnableRetryOnFailure` is SQL-Server-only regardless. Recorded so
a reviewer does not ask "where is the DB resilience?"

### D7: Development-only EF diagnostics via an environment seam

`EnableSensitiveDataLogging` and `EnableDetailedErrors` are turned on **only in
the Development environment**, so parameter values are visible while debugging
locally and never leak into a container/production log.
`AddPersistenceAdapter` currently takes only a connection string; it gains an
`isDevelopment` boolean (the composition root passes
`builder.Environment.IsDevelopment()`), rather than the adapter taking a
dependency on `IHostEnvironment`, keeping the adapter host-framework-agnostic.

### D8: Real-SQLite integration-test harness (reused by #4/#7)

Tests exercise the real SQLite provider against a **temp database file per
test**, not `:memory:` and not an in-memory fake (baseline: integration tests
hit the real SQLite file; a file also exercises the mounted-volume path #8
cares about). A disposable fixture creates a unique temp file, builds the
context options, applies migrations, and deletes the file on dispose. The
harness is written to be reusable so #4 (token/security tables) and #7
(settings) build their integration tests on it.

- *Alternative: shared-cache `:memory:` with a kept-open connection.* Rejected
  -- faster, but the baseline calls for the real file, and a file catches
  provider/migration issues an in-memory DB can hide.

## Risks / Trade-offs

- [Temp-file tests are slower and touch disk] -> acceptable; marker ops are
  tiny and the count is small. Parallel tests use distinct files, so no shared
  state.
- [WAL leaves `-wal`/`-shm` sidecar files on the volume] -> expected for WAL;
  documented so containerization (#8) mounts the directory, not just the `.db`.
- [Startup migration on a locked app] -> intentional; verified that no schema
  step needs the decrypted key. #4 must keep token *data* access behind unlock,
  not the schema.

## Open Questions

- None blocking. Busy-timeout duration and WAL specifics are implementation
  values set in D5; the 5-second command timeout is fixed by the proposal.
