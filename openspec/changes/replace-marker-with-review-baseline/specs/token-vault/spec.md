# token-vault Specification

## ADDED Requirements

### Requirement: Schema is created and evolved through migrations applied at startup

The context SHALL define the vault schema (security row and per-owner token
rows) through EF Core migrations, and the host SHALL apply pending migrations on
startup. Migration SHALL run before the app is unlocked, since the schema is not
secret and does not need the decrypted key. The store SHALL NOT use
`EnsureCreated`.

#### Scenario: Startup migrates the database

- **WHEN** the host starts against a database file with no schema or an older
  schema
- **THEN** pending migrations are applied and the vault tables exist before any
  request is served

#### Scenario: Migration does not require unlock

- **WHEN** migrations are applied at startup while the app is locked (no
  decrypted key available)
- **THEN** migration succeeds without needing the key

### Requirement: SQLite connection is configured for concurrent writes and fail-fast

The SQLite connection SHALL use WAL journal mode and a busy timeout so writes
issued close together serialize without one failing immediately, and SHALL set a
5-second command timeout so a write that cannot acquire its lock fails rather
than hanging the caller. The store SHALL NOT add a retrying execution strategy;
the busy timeout is the transient-failure handling.

#### Scenario: Concurrent writes both succeed under contention

- **WHEN** two writes to different rows are issued close together
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
fake.

#### Scenario: Vault round-trip through a real file

- **WHEN** a token is stored and then retrieved through the vault backed by a
  real temporary SQLite file with migrations applied
- **THEN** the retrieval returns the stored token, proving the real provider and
  schema round-trip
