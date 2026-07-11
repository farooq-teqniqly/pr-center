# state-store Specification

## Purpose

The `IStateStore` implementation over SQLite and the persistence foundation it
establishes. Marker semantics are fixed by
[pr-center-state.md](../../../docs/pr-center-state.md).

## Requirements

### Requirement: Last-seen markers persist per pull request and are never deleted

The persistence adapter SHALL store one last-seen marker per pull request,
keyed by the pull request's stable id (the same id `PullRequestFacts` carries),
holding the instant the user last looked. `SetLastSeenAsync` SHALL upsert:
insert a marker when none exists for the id, otherwise update its instant.
Markers SHALL never be deleted by the store. `GetLastSeenAsync` SHALL return the
stored instant, or null when no marker exists for the id. Both members SHALL
reject a null or whitespace pull request id.

#### Scenario: First set then get returns the instant

- **WHEN** `SetLastSeenAsync` is called for a pull request id that has no marker,
  then `GetLastSeenAsync` is called for the same id
- **THEN** the returned instant equals the one that was set

#### Scenario: Get with no marker returns null

- **WHEN** `GetLastSeenAsync` is called for a pull request id that was never set
- **THEN** it returns null

#### Scenario: Set again updates the same marker

- **WHEN** `SetLastSeenAsync` is called twice for the same id with different
  instants
- **THEN** `GetLastSeenAsync` returns the later-set instant and only one marker
  row exists for that id

#### Scenario: Guarded pull request id

- **WHEN** `GetLastSeenAsync` or `SetLastSeenAsync` is called with a null or
  whitespace pull request id
- **THEN** it throws `ArgumentException` (or `ArgumentNullException`) before any
  database access

### Requirement: Schema is created and evolved through migrations applied at startup

The context SHALL define the marker schema through an EF Core migration, and the
host SHALL apply pending migrations on startup. Migration SHALL run before the
app is unlocked, since the schema is not secret and does not need the decrypted
key. The store SHALL NOT use `EnsureCreated`.

#### Scenario: Startup migrates the database

- **WHEN** the host starts against a database file with no schema or an older
  schema
- **THEN** pending migrations are applied and the marker table exists before any
  request is served

#### Scenario: Migration does not require unlock

- **WHEN** migrations are applied at startup while the app is locked (no
  decrypted key available)
- **THEN** migration succeeds without needing the key

### Requirement: SQLite connection is configured for concurrent writes and fail-fast

The SQLite connection SHALL use WAL journal mode and a busy timeout so a
click-through write and a background-poll write serialize without one failing
immediately, and SHALL set a 5-second command timeout so a write that cannot
acquire its lock fails rather than hanging the caller. The store SHALL NOT add a
retrying execution strategy; the busy timeout is the transient-failure handling.

#### Scenario: Concurrent writes both succeed under contention

- **WHEN** two writes to different markers are issued close together
- **THEN** both complete successfully (serialized by SQLite, waited out by the
  busy timeout) rather than one failing with a lock error

#### Scenario: Command timeout bounds a stuck write

- **WHEN** a write cannot acquire the write lock within the command timeout
- **THEN** it fails with a timeout error rather than blocking indefinitely

### Requirement: Sensitive EF diagnostics are enabled only in Development

`EnableSensitiveDataLogging` and detailed errors SHALL be enabled only when the
host environment is Development, and disabled otherwise, so parameter values are
never written to logs in a production or container run.

#### Scenario: Development enables sensitive logging

- **WHEN** the persistence adapter is registered with the Development flag set
- **THEN** the context has sensitive data logging enabled

#### Scenario: Non-Development disables sensitive logging

- **WHEN** the persistence adapter is registered with the Development flag unset
- **THEN** the context does not have sensitive data logging enabled

### Requirement: Integration tests exercise the real SQLite file

The store SHALL be verified by integration tests that run against a real SQLite
database file (a unique temporary file per test, migrated on setup and removed
on teardown), using the real provider rather than an in-memory database or a
fake. The harness SHALL be reusable by later persistence changes.

#### Scenario: Marker round-trip through a real file

- **WHEN** a marker is set and then read through a `StateStore` backed by a real
  temporary SQLite file with migrations applied
- **THEN** the read returns the set instant, proving the real provider and
  schema round-trip
