# token-vault Specification

## Purpose
TBD - created by archiving change add-token-vault-and-lock. Update Purpose after archive.
## Requirements
### Requirement: App password establishes the vault
The system SHALL let the user set an app password once, deriving the encryption
key with Argon2id from the password and a randomly generated salt, and persisting
only the salt and the Argon2 parameters (memory, iterations, parallelism) plus an
encrypted sentinel. The password itself SHALL NOT be stored in any form.

#### Scenario: First-run password set
- **WHEN** the user sets an app password and no security row exists
- **THEN** the system generates a random salt, derives the key via Argon2id, encrypts a fixed known sentinel under that key with AES-GCM, and stores the salt, the Argon2 parameters, and the encrypted sentinel

#### Scenario: Password cannot be set twice
- **WHEN** the user attempts to set an app password and a security row already exists
- **THEN** the system rejects the request and leaves the existing security row unchanged

### Requirement: Password verification uses the encrypted sentinel
The system SHALL verify an entered password by re-deriving the key with the
stored salt and Argon2 parameters and decrypting the stored sentinel; a failed
AES-GCM authentication tag SHALL mean the password is wrong. Verification SHALL
succeed even when no owner tokens are stored yet.

#### Scenario: Correct password
- **WHEN** the entered password re-derives a key that decrypts the sentinel with a valid authentication tag
- **THEN** the system accepts the password

#### Scenario: Wrong password
- **WHEN** the entered password re-derives a key whose AES-GCM decryption of the sentinel fails the authentication tag
- **THEN** the system rejects the password and no key is retained

#### Scenario: Verification with zero tokens stored
- **WHEN** a password is verified immediately after being set, before any owner token is stored
- **THEN** verification still succeeds or fails purely on the sentinel, never erroring for lack of tokens

### Requirement: Tokens are stored encrypted at rest, one per owner
The system SHALL store at most one fine-grained PAT per owner, encrypted with
AES-GCM under the derived key, with a per-token random nonce and the
authentication tag persisted alongside the ciphertext. Storing a token for an
owner that already has one SHALL replace it.

#### Scenario: Store a new owner token
- **WHEN** a token is stored for an owner while the vault is unlocked
- **THEN** the system encrypts it with AES-GCM under the derived key using a fresh random nonce and persists ciphertext, nonce, and tag keyed by owner

#### Scenario: Replace an existing owner token
- **WHEN** a token is stored for an owner that already has a stored token
- **THEN** the system replaces the existing ciphertext, nonce, and tag for that owner

#### Scenario: Plaintext never persisted
- **WHEN** a token is stored
- **THEN** the plaintext token SHALL NOT appear in any persisted column

### Requirement: Token retrieval decrypts under the in-memory key
The system SHALL return the decrypted PAT for an owner while the vault is
unlocked, or null when no token is stored for that owner.

#### Scenario: Retrieve a stored token
- **WHEN** a token is requested for an owner that has one and the vault is unlocked
- **THEN** the system decrypts and returns the plaintext PAT

#### Scenario: Retrieve when none stored
- **WHEN** a token is requested for an owner that has no stored token and the vault is unlocked
- **THEN** the system returns null

### Requirement: Token access is refused while locked
The system SHALL refuse to store or retrieve tokens when the vault is not
unlocked, throwing a locked-vault error rather than returning data or a null.

#### Scenario: Store while locked
- **WHEN** a token store is attempted while the vault is Locked or Uninitialized
- **THEN** the system throws a locked-vault error and persists nothing

#### Scenario: Retrieve while locked
- **WHEN** a token retrieval is attempted while the vault is Locked or Uninitialized
- **THEN** the system throws a locked-vault error and returns no data

### Requirement: Reset wipes all vault data with no recovery
The system SHALL provide a reset that deletes every stored owner token and the
security row (salt, Argon2 parameters, sentinel), returning the vault to its
uninitialized condition. Reset SHALL NOT require the password and SHALL have no
recovery path.

#### Scenario: Reset clears tokens and security
- **WHEN** the user resets the vault
- **THEN** the system deletes all token rows and the security row, and a subsequent password set is treated as first-run

#### Scenario: Reset does not need unlock
- **WHEN** the user resets the vault while it is Locked
- **THEN** the reset succeeds without requiring the password

### Requirement: Vault enumerates owners with a stored token
The system SHALL enumerate the owners that currently have a stored token. This
enumeration is the authoritative owner list for polling. It SHALL read only the
plaintext owner key column, SHALL NOT decrypt any token, and SHALL work
regardless of lock state (lock gating of polling is the app lock's concern, not
the vault's).

#### Scenario: Owners with tokens are listed
- **WHEN** tokens are stored for one or more owners and the owner list is requested
- **THEN** the system returns exactly the owners that have a stored token

#### Scenario: Empty vault lists no owners
- **WHEN** no owner tokens are stored and the owner list is requested
- **THEN** the system returns an empty list

#### Scenario: Enumeration works while locked
- **WHEN** the owner list is requested while the vault is Locked or Uninitialized
- **THEN** the system returns the stored owners without error and without decrypting anything

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

